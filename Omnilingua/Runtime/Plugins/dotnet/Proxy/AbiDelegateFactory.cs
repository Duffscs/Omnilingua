using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.dotnet.Proxy;

/// <summary>Construit des types de délégués Cdecl uniquement à partir des types primitifs de l’ABI.</summary>
internal static class AbiDelegateFactory {
    private static readonly ModuleBuilder Module = AssemblyBuilder.DefineDynamicAssembly(new("Omnilingua.DynamicAbi"), AssemblyBuilderAccess.Run).DefineDynamicModule("Delegates");
    private static readonly Dictionary<string, Type> Cache = [];

    public static Type Create(Type returnType, Type[] parameters) {
        string key = returnType + ":" + string.Join(";", parameters.Select(type => type.ToString()));
        lock (Cache) {
            if (Cache.TryGetValue(key, out Type? known)) { return known; }
            TypeBuilder builder = Module.DefineType($"AbiDelegate{Cache.Count}", TypeAttributes.Public | TypeAttributes.Sealed, typeof(MulticastDelegate));
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(UnmanagedFunctionPointerAttribute).GetConstructor([typeof(CallingConvention)])!, [CallingConvention.Cdecl]));
            builder.DefineConstructor(MethodAttributes.Public | MethodAttributes.RTSpecialName, CallingConventions.Standard, [typeof(object), typeof(nint)])
                .SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            builder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, returnType, parameters)
                .SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            Type created = builder.CreateType()!;
            Cache.Add(key, created);
            return created;
        }
    }
}
