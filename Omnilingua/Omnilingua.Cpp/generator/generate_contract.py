"""Génère les exports C ABI depuis les véritables déclarations analysées par Clang."""

import argparse
from dataclasses import dataclass
import json
import os
from pathlib import Path
import re
import sys

try:
    from clang import cindex
except ImportError:
    raise SystemExit("libclang absent : installer generator/requirements.txt dans le Python de génération ou définir CppCodegenPythonPath.")


class GenerationError(Exception):
    pass


@dataclass
class AbiType:
    raw: str
    metadata: str
    cpp: str
    conversion: str = "{arg}"


def fail(cursor, message):
    raise GenerationError(f"{cursor.location.file}:{cursor.location.line}:{cursor.location.column}: {message}")


def abi_type(type, cursor, result=False):
    canonical = type.get_canonical()
    values = {
        cindex.TypeKind.VOID: AbiType("void", "Void", "void"),
        cindex.TypeKind.BOOL: AbiType("std::int32_t", "Boolean32", "bool", "::Omnilingua::Cpp::ReadValue<bool>({arg})"),
        cindex.TypeKind.INT: AbiType("std::int32_t", "Int32", "std::int32_t"),
        cindex.TypeKind.UINT: AbiType("std::uint32_t", "UInt32", "std::uint32_t"),
        cindex.TypeKind.ULONGLONG: AbiType("std::uint64_t", "UInt64", canonical.spelling),
    }
    if canonical.kind == cindex.TypeKind.ULONG and canonical.get_size() == 8:
        return AbiType("std::uint64_t", "UInt64", canonical.spelling)
    if canonical.kind in values:
        value = values[canonical.kind]
        if value.metadata == "Void" and not result:
            fail(cursor, "Un paramètre ne peut pas être void.")
        return value
    declaration = canonical.get_declaration()
    if not result and declaration.spelling == "basic_string_view" and canonical.get_template_argument_type(0).spelling == "char":
        parent = declaration.semantic_parent
        while parent and parent.kind == cindex.CursorKind.NAMESPACE and parent.spelling != "std":
            parent = parent.semantic_parent
        if parent and parent.spelling == "std":
            return AbiType("const char*", "Utf8", "std::string_view", "::Omnilingua::Cpp::ReadValue<std::string>({arg})")
    fail(cursor, f"Type non pris en charge : {type.spelling}. Types disponibles : void en retour, bool, int32, uint32, uint64 et std::string_view en entrée.")


def qualified_name(cursor):
    names = [cursor.spelling]
    parent = cursor.semantic_parent
    while parent and parent.kind != cindex.CursorKind.TRANSLATION_UNIT:
        if parent.kind != cindex.CursorKind.NAMESPACE or not parent.spelling:
            fail(cursor, "Le contrat doit appartenir à un namespace nommé ou à la portée globale.")
        names.insert(0, parent.spelling)
        parent = parent.semantic_parent
    return "::" + "::".join(names)


def contract_description(cursor, annotation):
    try:
        name, version = annotation.removeprefix("omnilingua.contract:").rsplit(":", 1)
        version = int(version)
    except ValueError:
        fail(cursor, "Annotation OMNILINGUA_PLUGIN_CONTRACT invalide.")
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_.]*", name) or not 1 <= version <= 0xFFFFFFFF:
        fail(cursor, "Nom ou version de contrat invalide.")
    methods = []
    names = set()
    for method in cursor.get_children():
        if method.kind == cindex.CursorKind.CXX_BASE_SPECIFIER and qualified_name(method.type.get_declaration()) != "::Omnilingua::Cpp::IService":
            fail(method, "L’héritage entre contrats générés n’est pas encore pris en charge.")
        if method.kind in (cindex.CursorKind.FUNCTION_TEMPLATE, cindex.CursorKind.FIELD_DECL):
            fail(method, "Un contrat exporté contient uniquement des méthodes virtuelles non génériques.")
        if method.kind != cindex.CursorKind.CXX_METHOD:
            continue
        if method.spelling in ("Ready", "Inject", "Invalidate"):
            fail(method, "Ce nom est réservé à l'adaptateur de services : Ready, Inject, Invalidate.")
        if method.access_specifier != cindex.AccessSpecifier.PUBLIC or method.is_static_method() or not method.is_pure_virtual_method():
            fail(method, "Chaque opération exportée doit être une méthode virtuelle publique pure.")
        if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", method.spelling) or method.spelling in names:
            fail(method, "Opérateur ou surcharge non pris en charge ; chaque nom d’opération doit être unique.")
        if method.type.is_function_variadic() or method.type.get_ref_qualifier() != cindex.RefQualifierKind.NONE or "volatile" in method.type.spelling:
            fail(method, "Méthode variadique, volatile ou qualifiée par référence non prise en charge.")
        arguments = list(method.get_arguments())
        if len(arguments) > 16:
            fail(method, "Seize paramètres au maximum sont pris en charge.")
        exception = method.exception_specification_kind
        if exception not in (cindex.ExceptionSpecificationKind.NONE, cindex.ExceptionSpecificationKind.BASIC_NOEXCEPT):
            fail(method, "Utiliser une spécification noexcept simple ou aucune spécification.")
        names.add(method.spelling)
        methods.append((method, abi_type(method.result_type, method, result=True),
                        [(argument, abi_type(argument.type, argument)) for argument in arguments],
                        exception == cindex.ExceptionSpecificationKind.BASIC_NOEXCEPT))
    if not methods:
        fail(cursor, "Le contrat ne déclare aucune opération exportée.")
    return name, version, qualified_name(cursor), methods


def generate_binding(contract):
    name, version, interface, methods = contract
    quote = lambda text: json.dumps(text, ensure_ascii=True)
    lines = [f"template<> class ExportBinding<{interface}> {{", f"    using Interface = {interface};",
             "    static constexpr std::size_t Capacity = 32;", "    inline static std::atomic<Interface*> _instances[Capacity]{};", "    std::size_t _slot;"]
    for method, result, arguments, noexcept in methods:
        types = ", ".join(type.cpp for _, type in arguments)
        qualifiers = (" const" if method.is_const_method() else "") + (" noexcept" if noexcept else "")
        lines += [f"    using Expected_{method.spelling} = {result.cpp} (Interface::*)({types}){qualifiers};",
                  f"    static_assert(std::is_same_v<decltype(&Interface::{method.spelling}), Expected_{method.spelling}>,",
                  '        "La signature analysee par Clang differe de celle compilee.");']
        parameters = ", ".join(f"{type.raw} arg{i}" for i, (_, type) in enumerate(arguments))
        call = ", ".join(type.conversion.format(arg=f"arg{i}") for i, (_, type) in enumerate(arguments))
        lines += [f"    template<std::size_t Slot> static {result.raw} OMNILINGUA_CALL Call_{method.spelling}({parameters}) noexcept {{", "        try {",
                  "            Interface* instance = _instances[Slot].load();",
                  '            if (!instance) { throw std::logic_error("Contrat non lie a une instance."); }']
        invocation = f"instance->{method.spelling}({call})"
        lines += [f"            {invocation};" if result.metadata == "Void" else
                  f"            return {invocation} ? 1 : 0;" if result.metadata == "Boolean32" else f"            return {invocation};"]
        lines += ["        } catch (const std::exception& error) {",
                  f"            ReportExportError({quote(name)}, {quote(method.spelling)}, error.what());",
                  "        } catch (...) {",
                  f"            ReportExportError({quote(name)}, {quote(method.spelling)}, \"Exception C++ inconnue.\");", "        }"]
        if result.metadata != "Void":
            lines.append("        return 0;")
        lines.append("    }")
    entries = []
    for method, result, arguments, _ in methods:
        parameters = ", ".join(f"In({quote(argument.spelling or f'argument{i}')}, {type.metadata})" for i, (argument, type) in enumerate(arguments))
        entries.append(f"Method<&Call_{method.spelling}<Slot>>({quote(method.spelling)}, Value({result.metadata})" + (", " + parameters if parameters else "") + ")")
    lines += [f"    template<std::size_t Slot> static Contract<{len(methods)}>& ContractAt() {{",
              f"        static Contract<{len(methods)}> contract{{{quote(name)}, {version},",
              "            " + ",\n            ".join(entries) + "};", "        return contract;", "    }",
              "    using Resolver = void*(*)(const char*, const char*);",
              "    template<std::size_t Slot> static void* ResolveAt(const char* contract, const char* method) noexcept { return ContractAt<Slot>().GetExport(contract, method); }",
              "    template<std::size_t... Slots> static std::array<Resolver, sizeof...(Slots)> Resolvers(std::index_sequence<Slots...>) { return std::array<Resolver, sizeof...(Slots)>{&ResolveAt<Slots>...}; }",
              "    inline static const auto _resolvers = Resolvers(std::make_index_sequence<Capacity>{});",
              "    inline static PluginMetadata _metadata{3, nullptr, 0, &ContractAt<0>().Description(), 1};", "public:",
              "    static const PluginContract& Description() noexcept { return ContractAt<0>().Description(); }",
              "    explicit ExportBinding(Interface& instance) {",
              "        for (_slot = 0; _slot < Capacity; ++_slot) {",
              "            Interface* expected = nullptr;",
              "            if (_instances[_slot].compare_exchange_strong(expected, &instance)) { return; }",
              "        }",
              '        throw std::logic_error("Trop de liaisons simultanees pour ce contrat (32 maximum).");',
              "    }", "    ~ExportBinding() { _instances[_slot].store(nullptr); }",
              "    ExportBinding(const ExportBinding&) = delete;", "    ExportBinding& operator=(const ExportBinding&) = delete;",
              "    const PluginMetadata* Metadata() const noexcept { return &_metadata; }",
              "    void* GetExport(const char* contract, const char* method) const noexcept { return _resolvers[_slot](contract, method); }", "};"]
    return "\n".join(lines)


def generate_import(contract):
    name, version, interface, methods = contract
    quote = lambda text: json.dumps(text, ensure_ascii=True)
    lines = [f"template<> class ImportBinding<{interface}> final : public {interface} {{"]
    for method, result, arguments, _ in methods:
        parameters = ", ".join(type.raw for _, type in arguments)
        lines += [f"    using Function_{method.spelling} = {result.raw}(OMNILINGUA_CALL*)({parameters});",
                  f"    std::atomic<Function_{method.spelling}> _{method.spelling}{{nullptr}};"]
    lines += ["public:", "    bool Ready() const noexcept { return " + " && ".join(f"_{method.spelling}.load() != nullptr" for method, _, _, _ in methods) + "; }",
              "    void Invalidate() noexcept {"]
    lines += [f"        _{method.spelling}.store(nullptr);" for method, _, _, _ in methods]
    lines += ["    }", "    bool Inject(const char* method, void* pointer) noexcept {", "        if (!method || !pointer) { return false; }"]
    for method, _, _, _ in methods:
        lines += [f"        if (!std::strcmp(method, {quote(method.spelling)})) {{",
                  f"            Function_{method.spelling} expected = nullptr;",
                  f"            return _{method.spelling}.compare_exchange_strong(expected, reinterpret_cast<Function_{method.spelling}>(pointer));", "        }"]
    lines += ["        return false;", "    }"]
    for method, result, arguments, noexcept in methods:
        parameters = ", ".join(f"{type.cpp} arg{i}" for i, (_, type) in enumerate(arguments))
        qualifiers = (" const" if method.is_const_method() else "") + (" noexcept" if noexcept else "")
        lines += [f"    {result.cpp} {method.spelling}({parameters}){qualifiers} override {{"]
        if noexcept: lines.append("        try {")
        lines += [f"        auto function = _{method.spelling}.load();",
                  '        if (!function) { throw std::logic_error("Service absent, incomplet ou arrete."); }']
        if any(type.metadata == "Utf8" for _, type in arguments): lines.append("        BufferStorage storage;")
        call = []
        for i, (_, type) in enumerate(arguments):
            if type.metadata == "Utf8":
                lines.append(f"        const char* value{i} = WriteValue<const char*>(std::string(arg{i}), storage);")
                call.append(f"value{i}")
            elif type.metadata == "Boolean32": call.append(f"(arg{i} ? 1 : 0)")
            else: call.append(f"arg{i}")
        invocation = "function(" + ", ".join(call) + ")"
        lines.append(f"        {invocation};" if result.metadata == "Void" else
                     f"        return ReadValue<bool>({invocation});" if result.metadata == "Boolean32" else f"        return {invocation};")
        if noexcept:
            lines += ["        } catch (const std::exception& error) {",
                      f"            ReportExportError({quote(name)}, {quote(method.spelling)}, error.what());",
                      "        } catch (...) {",
                      f"            ReportExportError({quote(name)}, {quote(method.spelling)}, \"Exception C++ inconnue.\");", "        }"]
            if result.metadata != "Void": lines.append("        return 0;")
        lines.append("    }")
    return "\n".join(lines + ["};"])


def derives_from(cursor, name):
    return any(qualified_name(base.type.get_declaration()) == name or derives_from(base.type.get_declaration(), name)
               for base in cursor.get_children() if base.kind == cindex.CursorKind.CXX_BASE_SPECIFIER)


def plugin_description(cursor, annotations, contracts, definitions):
    if cursor.kind not in (cindex.CursorKind.CLASS_DECL, cindex.CursorKind.STRUCT_DECL) or not cursor.is_definition():
        fail(cursor, "Annoter une classe plugin concrete non generique.")
    if cursor.is_abstract_record() or not derives_from(cursor, "::Omnilingua::Cpp::IPlugin"):
        fail(cursor, "La classe annotee doit implementer Omnilingua::Cpp::IPlugin et etre concrete.")
    interface_names = {contract[2]: contract for contract in contracts}
    declarations = []
    names = set()
    imported_types = set()
    for annotation in annotations:
        direction, requested = annotation.removeprefix("omnilingua.plugin.").split(":", 1)
        if direction == "import":
            requested, _, optional = requested.partition(":")
            if optional not in ("", "true", "false"):
                fail(cursor, "L'option d'import doit etre true ou false.")
            if optional == "true": direction = "optional"
        candidates = ["::" + requested.removeprefix("::")]
        if not requested.startswith("::"):
            namespace = qualified_name(cursor).rsplit("::", 1)[0]
            local = []
            while namespace:
                local.append(namespace + "::" + requested)
                namespace = namespace.rsplit("::", 1)[0]
            candidates = local + candidates
        match = next((interface_names[name] for name in candidates if name in interface_names), None)
        if match is None:
            definition = next((definitions[name] for name in candidates if name in definitions), None)
            if definition is None:
                fail(cursor, "Interface introuvable : " + requested)
            match = contract_description(definition, "omnilingua.contract:" + definition.spelling + ":1")
            contracts.append(match)
            interface_names[match[2]] = match
        interface_cursor = match[3][0][0].semantic_parent
        if not derives_from(interface_cursor, "::Omnilingua::Cpp::IService"):
            fail(cursor, "L'interface doit deriver de Omnilingua::Cpp::IService : " + requested)
        key = ("export" if direction == "export" else "import", match[0])
        if key in names:
            fail(cursor, "Contrat duplique : " + match[0])
        names.add(key)
        if direction != "export":
            if match[2] in imported_types: fail(cursor, "Interface importee plusieurs fois.")
            imported_types.add(match[2])
        declarations.append((direction, match))
    return qualified_name(cursor), declarations


def generate_plugin(plugin):
    implementation, declarations = plugin
    exports = [contract for direction, contract in declarations if direction == "export"]
    lines = [f"template<> struct PluginDeclaration<{implementation}> {{",
             "    static void Configure(PluginBuilder& builder) {"]
    for direction, contract in declarations:
        interface = contract[2]
        lines.append(f"        builder.DeclareExport<{interface}>();" if direction == "export" else
                     f"        builder.Import<{interface}>({'true' if direction == 'optional' else 'false'});")
    lines += ["    }", "    static void Bind([[maybe_unused]] PluginBuilder& builder, const std::vector<IService*>& objects) {",
              "        for (IService* object : objects) {"]
    matches = " || ".join(f"dynamic_cast<{contract[2]}*>(object) != nullptr" for contract in exports) or "false"
    lines += [f"            if (!object || !({matches})) {{ throw std::logic_error(\"Objet exporte sans contrat annonce.\"); }}", "        }"]
    lines += [f"        builder.BindExport<{contract[2]}>(objects);" for contract in exports]
    return "\n".join(lines + ["    }", "};"])


def generate(header, output, arguments):
    header, output = header.resolve(), output.resolve()
    library = os.environ.get("LIBCLANG_LIBRARY_FILE")
    if library:
        cindex.Config.set_library_file(library)
    unit = cindex.Index.create().parse(str(header), args=arguments,
        options=cindex.TranslationUnit.PARSE_SKIP_FUNCTION_BODIES)
    errors = [str(diagnostic) for diagnostic in unit.diagnostics if diagnostic.severity >= cindex.Diagnostic.Error]
    if errors:
        raise GenerationError("\n".join(errors))
    contracts = []
    plugin_cursors = []
    definitions = {}
    def visit(cursor):
        for child in cursor.get_children():
            if not child.location.file:
                continue
            if child.kind in (cindex.CursorKind.CLASS_DECL, cindex.CursorKind.STRUCT_DECL) and child.is_definition():
                try: definitions[qualified_name(child)] = child
                except GenerationError: pass
            annotations = [item.spelling for item in child.get_children() if item.kind == cindex.CursorKind.ANNOTATE_ATTR and item.spelling.startswith("omnilingua.contract:")]
            if annotations:
                if child.kind not in (cindex.CursorKind.CLASS_DECL, cindex.CursorKind.STRUCT_DECL) or not child.is_definition() or len(annotations) != 1:
                    fail(child, "Annoter la définition complète d’une interface non générique.")
                contracts.append(contract_description(child, annotations[0]))
            plugin_annotations = [item.spelling for item in child.get_children() if item.kind == cindex.CursorKind.ANNOTATE_ATTR and item.spelling.startswith("omnilingua.plugin.")]
            if plugin_annotations and Path(child.location.file.name).resolve() == header:
                plugin_cursors.append((child, plugin_annotations))
            if child.kind == cindex.CursorKind.NAMESPACE:
                visit(child)
    visit(unit.cursor)
    if not contracts and not plugin_cursors:
        raise GenerationError(f"{header}: aucune interface OMNILINGUA_PLUGIN_CONTRACT trouvée.")
    plugins = [plugin_description(cursor, annotations, contracts, definitions) for cursor, annotations in plugin_cursors]
    if plugins:
        used = {contract[2] for _, declarations in plugins for _, contract in declarations}
        contracts = [contract for contract in contracts if contract[2] in used]
    else:
        contracts = [contract for contract in contracts if Path(contract[3][0][0].location.file.name).resolve() == header]
    names = [contract[0] for contract in contracts]
    if len(set(names)) != len(names):
        raise GenerationError("Plusieurs contrats utilisent le même nom dans ce header.")
    include = os.path.relpath(header, output.parent).replace("\\", "/")
    content = '// Fichier généré depuis le header annoté ; ne pas modifier manuellement.\n#pragma once\n'
    content += f'#include "{include}"\n#include <Omnilingua/Plugin.h>\n\nnamespace Omnilingua::Cpp {{\n'
    content += "\n\n".join(generate_binding(contract) + "\n\n" + generate_import(contract) for contract in contracts) + "\n\n" + "\n\n".join(generate_plugin(plugin) for plugin in plugins) + "\n}\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    if not output.exists() or output.read_text(encoding="utf-8") != content:
        temporary = output.with_suffix(output.suffix + ".tmp")
        temporary.write_text(content, encoding="utf-8")
        temporary.replace(output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--header", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--include", action="append", default=[])
    parser.add_argument("--system-include", action="append", default=[])
    parser.add_argument("--extra-arg", action="append", default=[])
    args = parser.parse_args()
    arguments = ["-x", "c++", "-std=c++20", "-Wno-pragma-once-outside-header"]
    if os.name == "nt":
        arguments += ["--target=x86_64-pc-windows-msvc", "-fms-extensions", "-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH"]
    arguments += ["-I" + include for include in args.include]
    for include in args.system_include:
        arguments += ["-isystem", include]
    arguments += args.extra_arg
    try:
        generate(args.header, args.output, arguments)
    except (GenerationError, cindex.LibclangError, cindex.TranslationUnitLoadError) as error:
        print(f"Génération C++ refusée : {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
