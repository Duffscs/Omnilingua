using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Omnilingua.Dotnet.SourceGen;

public sealed partial class PluginContractGenerator {
    private static AnalysisResult PrepareContract(INamedTypeSymbol type, AttributeData attribute, CancellationToken cancellation) {
        Location location = type.Locations.First();
        try {
            cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Interface || type.IsGenericType || type.ContainingType is not null) {
                throw new ContractException("Le contrat exige une interface non générique et non imbriquée.");
            }
            string contract = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Name").Value.Value as string ?? type.Name;
            uint version = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Version").Value.Value is uint value ? value : 1;
            if (string.IsNullOrWhiteSpace(contract) || contract!.Any(char.IsControl) || version == 0) {
                throw new ContractException("Le contrat doit avoir un nom non vide sans caractère de contrôle et une version positive.");
            }
            TypeCollector collector = new(cancellation);
            List<MethodModel> methods = [];
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (IMethodSymbol method in new[] { type }.Concat(type.AllInterfaces).SelectMany(contractType => contractType.GetMembers()).OfType<IMethodSymbol>()
                .Where(method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsImplicitlyDeclared)
                .GroupBy(method => method.Name + "(" + string.Join(",", method.Parameters.Select(parameter => parameter.RefKind + ":" + Reference(parameter.Type))) + ")=>" + Reference(method.ReturnType))
                .Select(group => group.First())
                .OrderBy(method => method.Name, StringComparer.Ordinal)) {
                cancellation.ThrowIfCancellationRequested();
                if (method.IsStatic || method.IsAsync || method.IsGenericMethod || !names.Add(method.Name) || method.ReturnsByRef || method.ReturnsByRefReadonly) {
                    throw new ContractException($"{method.Name} : les méthodes statiques, async ou génériques, surcharges et retours ref ne sont pas pris en charge.");
                }
                if (method.Name is "BindGeneratedExport") {
                    throw new ContractException($"{method.Name} est réservé au générateur de contrat.");
                }
                List<ParameterModel> parameters = [];
                if (method.Parameters.Length > 16) { throw new ContractException($"{method.Name} : seize paramètres au maximum."); }
                foreach (IParameterSymbol parameter in method.Parameters) {
                    if (parameter.RefKind is not (RefKind.None or RefKind.Out)) {
                        throw new ContractException($"{method.Name}.{parameter.Name} : seul out est pris en charge parmi les paramètres par référence.");
                    }
                    RejectNullableValue(parameter.Type, $"{method.Name}.{parameter.Name}");
                    parameters.Add(new(parameter.Name, Reference(parameter.Type), collector.Add(parameter.Type, collectionReturn: true), parameter.RefKind == RefKind.Out));
                }
                RejectNullableValue(method.ReturnType, method.Name);
                methods.Add(new(method.Name, Escape(method.Name), Reference(method.ReturnType),
                    collector.Add(method.ReturnType, collectionReturn: true), parameters.ToImmutableArray()));
            }
            if (methods.Count == 0) { throw new ContractException($"{type.Name} ne déclare aucune méthode publique exportable."); }
            string ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
            return new(new(ns, Reference(type), type.DeclaredAccessibility == Accessibility.Public, ns + "." + type.Name,
                contract!, version, methods.ToImmutableArray(), collector.Types.ToImmutableArray()), location, null);
        } catch (ContractException exception) {
            return new(null, location, exception.Message);
        }
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    private static string Reference(ITypeSymbol type) => type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static bool NullableValue(ITypeSymbol type) => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    private static void RejectNullableValue(ITypeSymbol type, string method) {
        if (NullableValue(type)) { throw new ContractException($"{method} : un scalaire nullable doit appartenir à un modèle, avec un bit de présence ABI."); }
    }
    private sealed class ContractException(string message) : Exception(message);

    private sealed class TypeCollector(CancellationToken cancellation) {
        private readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _recordNames = new(StringComparer.Ordinal);
        public List<TypeModel> Types { get; } = [];

        public string Add(ITypeSymbol original, bool collectionReturn) {
            cancellation.ThrowIfCancellationRequested();
            ITypeSymbol type = NullableValue(original) ? ((INamedTypeSymbol)original).TypeArguments[0] : original;
            string key = Reference(type) + (collectionReturn && CollectionElement(type) is not null ? ":sequence" : ":value");
            if (_visiting.Contains(key)) { throw new ContractException($"Cycle de modèles non pris en charge : {Reference(type)}."); }
            if (_ids.TryGetValue(key, out string? known)) { return known; }
            string id = "_abi" + _ids.Count;
            _ids.Add(key, id); _visiting.Add(key);
            string? primitive = type.SpecialType switch {
                SpecialType.System_Void => "Void", SpecialType.System_Boolean => "Boolean32", SpecialType.System_Int32 => "Int32",
                SpecialType.System_UInt32 => "UInt32", SpecialType.System_UInt64 => "UInt64", SpecialType.System_String => "Utf8", _ => null
            };
            TypeModel model;
            if (primitive is not null) {
                model = new(id, primitive.ToLowerInvariant(), primitive, [], null, []);
            } else if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumeration) {
                string underlying = enumeration.EnumUnderlyingType!.SpecialType switch {
                    SpecialType.System_Int32 => "Int32", SpecialType.System_UInt32 => "UInt32", SpecialType.System_UInt64 => "UInt64",
                    _ => throw new ContractException($"{type.Name} : l’enum doit reposer sur int, uint ou ulong.")
                };
                ImmutableArray<EnumValueModel> values = enumeration.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue)
                    .Select(field => new EnumValueModel(field.Name, Convert.ToString(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture)!)).ToImmutableArray();
                model = new(id, type.Name, underlying, [], null, values);
            } else if (CollectionElement(type) is ITypeSymbol element) {
                if (NullableValue(element) || (element.TypeKind == TypeKind.Class && element.SpecialType != SpecialType.System_String && element.NullableAnnotation == NullableAnnotation.Annotated)) {
                    throw new ContractException($"{Reference(type)} : éléments nullable non pris en charge, sauf chaînes.");
                }
                if (CollectionElement(element) is not null) { throw new ContractException("Les collections de collections ne sont pas prises en charge."); }
                string child = Add(element, false);
                model = new(id, element.Name + "[]", collectionReturn ? "Sequence" : "Array", [], child, []);
            } else if (type is INamedTypeSymbol record && type.TypeKind == TypeKind.Class && record.SpecialType == SpecialType.None && !record.IsGenericType &&
                !record.IsAbstract && record.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0)) {
                string fullName = Reference(record);
                if (_recordNames.TryGetValue(record.Name, out string? existing) && existing != fullName) {
                    throw new ContractException($"Noms ABI ambigus : {existing} et {fullName}.");
                }
                _recordNames[record.Name] = fullName;
                Dictionary<string, IPropertySymbol> properties = new(StringComparer.Ordinal);
                for (INamedTypeSymbol? current = record; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType) {
                    if (current.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)) {
                        throw new ContractException($"{record.Name} : utiliser des propriétés publiques plutôt que des champs publics pour le modèle ABI.");
                    }
                    foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public)) {
                        if (!properties.ContainsKey(property.Name)) { properties.Add(property.Name, property); }
                    }
                }
                List<FieldModel> fields = [];
                foreach (IPropertySymbol property in properties.Values.OrderBy(property => property.Name, StringComparer.Ordinal)) {
                    if (property.IsIndexer || property.GetMethod?.DeclaredAccessibility != Accessibility.Public || property.SetMethod?.DeclaredAccessibility != Accessibility.Public) {
                        throw new ContractException($"{record.Name}.{property.Name} : propriété lisible et modifiable requise pour la copie ABI.");
                    }
                    bool optional = NullableValue(property.Type) || (property.Type.IsReferenceType && property.Type.SpecialType != SpecialType.System_String &&
                        property.NullableAnnotation == NullableAnnotation.Annotated);
                    fields.Add(new(property.Name, Add(property.Type, false), optional));
                }
                if (fields.Count == 0) { throw new ContractException($"{record.Name} ne contient aucune propriété ABI."); }
                model = new(id, record.Name, "Record", fields.ToImmutableArray(), null, []);
            } else {
                throw new ContractException($"Type non pris en charge par l’ABI : {Reference(type)}. Utiliser les scalaires, enums int/uint/ulong, modèles à constructeur public sans paramètre et collections simples.");
            }
            Types.Add(model); _visiting.Remove(key); return id;
        }

        private static ITypeSymbol? CollectionElement(ITypeSymbol type) => type is IArrayTypeSymbol array && array.Rank == 1 ? array.ElementType :
            type is INamedTypeSymbol named && named.Arity == 1 && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            named.Name is "List" or "IReadOnlyList" ? named.TypeArguments[0] : null;
    }
}
