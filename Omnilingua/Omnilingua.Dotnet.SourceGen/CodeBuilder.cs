using System;
using System.Collections.Generic;
using System.Text;

namespace Omnilingua.Dotnet.SourceGen;

public enum Visibility {
    Public,
    Private,
    Protected,
    Internal
}

/// <summary>Construit le code C# g?n?r? avec indentation et gestion des blocs.</summary>
public sealed class CodeBuilder {
    private readonly StringBuilder _builder = new();
    private readonly Stack<string> _blockStack = new();

    private int _indent = 0;

    public CodeBuilder AddUsing(string ns) {
        _builder.AppendLine($"using {ns};");
        return this;
    }

    public CodeBuilder AddLine(string line) {
        _builder.AppendLine($"{Indent()}{line}");
        return this;
    }

    public CodeBuilder AddNamespace(string name) {
        _builder.AppendLine();
        _builder.AppendLine($"namespace {name}" + " {");
        _blockStack.Push("namespace");
        _indent++;
        return this;
    }

    public CodeBuilder AddClass(string name, ClassOptions? options = null) {
        options ??= new ClassOptions();
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Modifiers} class {name}{FormatGeneric(options.Generics)}{FormatInheritance(options.BaseTypes)}" + " {");
        _blockStack.Push("class");
        _indent++;
        return this;
    }

    public CodeBuilder AddRecord(string name, ClassOptions? options = null) {
        options ??= new ClassOptions();
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Modifiers} record {name}{FormatGeneric(options.Generics)}{FormatInheritance(options.BaseTypes)}" + " {");
        _blockStack.Push("record");
        _indent++;
        return this;
    }

    public CodeBuilder AddStruct(string name, ClassOptions? options = null) {
        options ??= new ClassOptions();
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} struct {name}{FormatGeneric(options.Generics)}{FormatInheritance(options.BaseTypes)}" + " {");
        _blockStack.Push("struct");
        _indent++;
        return this;
    }

    public CodeBuilder AddInterface(string name, ClassOptions options) {
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Modifiers} interface {name}{FormatGeneric(options.Generics)}{FormatInheritance(options.BaseTypes)}" + " {");
        _blockStack.Push("interface");
        _indent++;
        return this;
    }

    public CodeBuilder AddField(string type, string name, MemberOptions options) {
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Modifiers} {type} {name}{(options.InitialValue != null ? " = " + options.InitialValue + ";" : ";")}");
        return this;
    }

    public CodeBuilder AddProperty(string type, string name, PropertyOptions options) {
        _builder.Append($"{Indent()}{ToKeyword(options.Visibility)} {type} {name} {{ ");
        if (options.HasGet)
            _builder.Append("get; ");
        if (options.HasSet)
            _builder.Append("set; ");
        if (options.HasInit)
            _builder.Append("init; ");
        _builder.AppendLine("}");
        return this;
    }

    public CodeBuilder AddConstructor(ConstructorOptions? options = null) {
        options ??= new ConstructorOptions();
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Name}({FormatParameters(options.Parameters)})" + " {");
        _blockStack.Push("constructor");
        _indent++;
        return this;
    }

    public CodeBuilder AddMethod(string name, MethodOptions? options = null) {
        options ??= new MethodOptions();
        _builder.AppendLine();
        _builder.AppendLine($"{Indent()}{ToKeyword(options.Visibility)} {options.Modifiers} {options.ReturnType} {name}{FormatGeneric(options.Generics)}({FormatParameters(options.Parameters)})" + " {");
        _blockStack.Push($"method {name}");
        _indent++;
        return this;
    }

    public CodeBuilder For(string condition, Action body) {
        _builder.AppendLine($"{Indent()}for ({condition})" + " {");
        _indent++;
        body();
        _indent--;
        _builder.AppendLine($"{Indent()}}}");
        return this;
    }

    public CodeBuilder Foreach(string condition, Action body) {
        _builder.AppendLine($"{Indent()}foreach ({condition})" + " {");
        _indent++;
        body();
        _indent--;
        _builder.AppendLine($"{Indent()}}}");
        return this;
    }

    public CodeBuilder If(string condition, Action body) {
        _builder.AppendLine($"{Indent()}if ({condition})" + " {");
        _indent++;
        body();
        _indent--;
        _builder.AppendLine($"{Indent()}}}");
        return this;
    }

    public CodeBuilder Else(Action body) {
        _builder.AppendLine($"{Indent()}else" + " {");
        _indent++;
        body();
        _indent--;
        _builder.AppendLine($"{Indent()}}}");
        return this;
    }

    public CodeBuilder EndBlock() {
        if (_blockStack.Count > 0) {
            _indent--;
            _builder.AppendLine($"{Indent()}}}");
            _blockStack.Pop();
        }
        return this;
    }

    /// <summary>Ouvre un bloc explicite pour les initialisations et les lambdas générées.</summary>
    public CodeBuilder BeginBlock(string declaration) {
        AddLine(declaration + " {");
        _blockStack.Push("block");
        _indent++;
        return this;
    }

    public string Build() {
        while (_blockStack.Count > 0)
            EndBlock();
        return _builder.ToString();
    }

    private string Indent() => new(' ', _indent * 4);

    private static string ToKeyword(Visibility v) => v switch {
        Visibility.Public => "public",
        Visibility.Private => "private",
        Visibility.Protected => "protected",
        Visibility.Internal => "internal",
        _ => ""
    };

    private static string FormatParameters(IReadOnlyList<(string Type, string Name)> parameters) {
        if (parameters == null || parameters.Count == 0)
            return "";
        List<string> parts = new();
        foreach ((string Type, string Name) p in parameters)
            parts.Add($"{p.Type} {p.Name}");
        return string.Join(", ", parts);
    }

    private static string FormatInheritance(IReadOnlyList<string> baseTypes) {
        if (baseTypes == null || baseTypes.Count == 0)
            return "";
        return " : " + string.Join(", ", baseTypes);
    }

    private static string FormatGeneric(IReadOnlyList<string> generics) {
        if (generics == null || generics.Count == 0)
            return "";
        return "<" + string.Join(", ", generics) + ">";
    }
}

public sealed class ClassOptions {
    public string Modifiers { get; init; } = "";
    public Visibility Visibility { get; init; } = Visibility.Public;
    public List<string> BaseTypes { get; init; } = [];
    public List<string> Generics { get; init; } = [];
}

public sealed class MemberOptions {
    public string Modifiers { get; init; } = "";
    public Visibility Visibility { get; init; } = Visibility.Private;
    public string? InitialValue { get; init; }
}

public sealed class PropertyOptions {
    public Visibility Visibility { get; init; } = Visibility.Public;
    public bool HasGet { get; init; } = true;
    public bool HasSet { get; init; } = true;
    public bool HasInit { get; init; }
}

public sealed class ConstructorOptions {
    public Visibility Visibility { get; init; } = Visibility.Public;
    public string Name { get; init; } = "";
    public List<(string Type, string Name)> Parameters { get; init; } = [];
}

public sealed class MethodOptions {
    public string Modifiers { get; init; } = "";
    public Visibility Visibility { get; init; } = Visibility.Public;
    public string ReturnType { get; init; } = "void";
    public List<string> Generics { get; init; } = [];
    public List<(string Type, string Name)> Parameters { get; init; } = [];
}
