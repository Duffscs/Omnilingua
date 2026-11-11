using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Omnilingua.Dotnet;

namespace Omnilingua.Plugins.dotnet.Proxy;

/// <summary>Implémente une interface locale uniquement à partir des descriptions et des fonctions du fournisseur.</summary>
public class ContractProxy : DispatchProxy {
    private readonly Dictionary<string, BoundMethod> _methods = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Vérifie toutes les signatures avant de créer un objet injectable dans le consommateur.</summary>
    public static object Create(Type interfaceType, ContractDescription contract, Func<string, nint> resolve) {
        if (!interfaceType.IsInterface || interfaceType.ContainsGenericParameters) { throw new InvalidOperationException("Une interface fermée est requise pour l’injection."); }
        Dictionary<string, BoundMethod> methods = new(StringComparer.Ordinal);
        foreach (MethodInfo method in new[] { interfaceType }.Concat(interfaceType.GetInterfaces()).SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)).DistinctBy(method => method.ToString())) {
            if (method.IsGenericMethod || methods.ContainsKey(method.Name)) { throw new InvalidOperationException($"Méthode générique ou surchargée non prise en charge : {method.Name}."); }
            AbiMethodDescription signature = contract.Signatures?.SingleOrDefault(candidate => candidate.Name == method.Name)
                ?? throw new MissingMethodException($"{contract.Name}.{method.Name} : description ABI absente.");
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != signature.Parameters.Count) { throw new InvalidOperationException($"{contract.Name}.{method.Name} : nombre de paramètres incompatible."); }
            AbiMarshaller.Validate(signature.ReturnType, method.ReturnType);
            Type[] rawTypes = new Type[parameters.Length];
            for (int index = 0; index < parameters.Length; index++) {
                AbiParameterDescription parameter = signature.Parameters[index];
                Type local = parameters[index].ParameterType;
                if (parameter.Passing == AbiPassing.Out) {
                    if (!local.IsByRef || !parameters[index].IsOut) { throw new InvalidOperationException($"{method.Name}.{parameters[index].Name} doit être un paramètre out."); }
                    local = local.GetElementType()!;
                } else if (local.IsByRef) { throw new InvalidOperationException($"Paramètre ref non pris en charge : {method.Name}."); }
                if (parameter.Type.Kind is AbiKind.Void or AbiKind.Array || (parameter.Passing == AbiPassing.Reference && parameter.Type.Kind is not (AbiKind.Record or AbiKind.Sequence))) {
                    throw new InvalidOperationException($"Passage ABI non pris en charge : {method.Name}.{parameters[index].Name}.");
                }
                AbiMarshaller.Validate(parameter.Type, local);
                rawTypes[index] = AbiMarshaller.RawType(parameter.Type, parameter.Passing);
            }
            nint pointer = resolve(method.Name);
            if (pointer == 0) { throw new MissingMethodException($"{contract.Name}.{method.Name} : export nul."); }
            Type delegateType = AbiDelegateFactory.Create(AbiMarshaller.RawType(signature.ReturnType, signature.ReturnPassing), rawTypes);
            methods.Add(method.Name, new(signature, method, Marshal.GetDelegateForFunctionPointer(pointer, delegateType), rawTypes));
        }
        using IDisposable? reflectionContext = AssemblyLoadContext.EnterContextualReflection(interfaceType.Assembly);
        // DispatchProxy choisit son assembly à partir du type de base : une base locale
        // évite de confondre deux interfaces homonymes chargées dans des contextes distincts.
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("Omnilingua.ProxyBase." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        TypeBuilder baseBuilder = assembly.DefineDynamicModule("Proxy").DefineType("ContextualProxy", TypeAttributes.Public, typeof(ContractProxy));
        baseBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        Type proxyBase = baseBuilder.CreateType()!;
        object proxy = DispatchProxy.Create(interfaceType, proxyBase);
        ContractProxy implementation = (ContractProxy)proxy;
        foreach ((string name, BoundMethod method) in methods) { implementation._methods.Add(name, method); }
        return proxy;
    }

    /// <summary>Convertit les valeurs, appelle le pointeur puis copie immédiatement les sorties empruntées.</summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        ArgumentNullException.ThrowIfNull(targetMethod);
        BoundMethod method = _methods[targetMethod.Name];
        args ??= [];
        lock (_gate) {
            using AbiMarshaller marshaller = new();
            object?[] abi = new object?[args.Length];
            for (int index = 0; index < args.Length; index++) {
                AbiParameterDescription parameter = method.Description.Parameters[index];
                abi[index] = parameter.Passing == AbiPassing.Out ? Activator.CreateInstance(method.RawTypes[index].GetElementType()!) : marshaller.Write(parameter.Type, args[index]);
            }
            object? result;
            try { result = method.Function.DynamicInvoke(abi); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null) {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw;
            }
            ParameterInfo[] parameters = method.Method.GetParameters();
            for (int index = 0; index < args.Length; index++) {
                if (method.Description.Parameters[index].Passing == AbiPassing.Out) {
                    args[index] = marshaller.Read(method.Description.Parameters[index].Type, parameters[index].ParameterType, abi[index]);
                }
            }
            object? converted = marshaller.Read(method.Description.ReturnType, targetMethod.ReturnType, result);
            if (converted is null && !targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void) &&
                new NullabilityInfoContext().Create(targetMethod.ReturnParameter).ReadState == NullabilityState.NotNull) {
                throw new InvalidDataException($"Le fournisseur a retourné null pour {targetMethod.Name}, déclaré non nullable.");
            }
            return converted;
        }
    }

    private sealed record BoundMethod(AbiMethodDescription Description, MethodInfo Method, Delegate Function, Type[] RawTypes);
}
