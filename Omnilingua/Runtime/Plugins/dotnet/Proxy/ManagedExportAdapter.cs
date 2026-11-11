using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Omnilingua.Dotnet;

namespace Omnilingua.Plugins.dotnet.Proxy;

/// <summary>Conserve les callbacks compilés et leurs instantanés de sortie jusqu'à la fin de leur utilisation.</summary>
internal sealed class ManagedExportAdapter : IDisposable {
    private readonly Dictionary<string, BoundExport> _methods = [];

    public ManagedExportAdapter(ManagedContractExport export) {
        try {
            foreach (ManagedMethodExport method in export.Methods) {
                _methods.Add(method.Description.Name, new BoundExport(export.Description.Name, method));
            }
        } catch { Dispose(); throw; }
    }

    public nint Resolve(string method) => _methods.TryGetValue(method, out BoundExport? bound) ? bound.Pointer : 0;
    public void Dispose() { foreach (BoundExport method in _methods.Values) { method.Dispose(); } }

    private sealed class BoundExport : IDisposable {
        private readonly string _contract;
        private readonly ManagedMethodExport _method;
        private readonly ThreadLocal<AbiMarshaller?> _snapshots = new(() => null, true);
        private readonly Delegate _callback;
        public nint Pointer { get; }

        public BoundExport(string contract, ManagedMethodExport method) {
            _contract = contract;
            _method = method;
            AbiMarshaller.Validate(method.Description.ReturnType, method.ReturnType);
            if (method.ParameterTypes.Count != method.Description.Parameters.Count) { throw new InvalidOperationException("Nombre de paramètres exportés incompatible."); }
            Type[] rawTypes = method.Description.Parameters.Select(parameter => AbiMarshaller.RawType(parameter.Type, parameter.Passing)).ToArray();
            for (int index = 0; index < rawTypes.Length; index++) { AbiMarshaller.Validate(method.Description.Parameters[index].Type, method.ParameterTypes[index]); }
            Type returnType = AbiMarshaller.RawType(method.Description.ReturnType, method.Description.ReturnPassing);
            ParameterExpression[] parameters = rawTypes.Select((type, index) => Expression.Parameter(type, "p" + index)).ToArray();
            ParameterExpression arguments = Expression.Variable(typeof(object[]), "arguments");
            ParameterExpression result = Expression.Variable(typeof(object), "result");
            List<Expression> body = [Expression.Assign(arguments, Expression.NewArrayInit(typeof(object), parameters.Select((parameter, index) =>
                method.Description.Parameters[index].Passing == AbiPassing.Out ? (Expression)Expression.Constant(null, typeof(object)) : Expression.Convert(parameter, typeof(object)))))];
            body.Add(Expression.Assign(result, Expression.Call(Expression.Constant(this), typeof(BoundExport).GetMethod(nameof(Invoke))!, arguments)));
            for (int index = 0; index < parameters.Length; index++) {
                if (method.Description.Parameters[index].Passing == AbiPassing.Out) {
                    body.Add(Expression.Assign(parameters[index], Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(index)), rawTypes[index].GetElementType()!)));
                }
            }
            body.Add(returnType == typeof(void) ? Expression.Empty() : Expression.Convert(result, returnType));
            _callback = Expression.Lambda(AbiDelegateFactory.Create(returnType, rawTypes), Expression.Block([arguments, result], body), parameters).Compile();
            Pointer = Marshal.GetFunctionPointerForDelegate(_callback);
        }

        public object? Invoke(object?[] raw) {
            AbiMarshaller next = new();
            try {
                object?[] values = new object?[raw.Length];
                for (int index = 0; index < raw.Length; index++) {
                    AbiParameterDescription parameter = _method.Description.Parameters[index];
                    if (parameter.Passing != AbiPassing.Out) { values[index] = next.Read(parameter.Type, _method.ParameterTypes[index], raw[index]); }
                }
                object? value = _method.Invoke(values);
                for (int index = 0; index < raw.Length; index++) {
                    AbiParameterDescription parameter = _method.Description.Parameters[index];
                    if (parameter.Passing == AbiPassing.Out) { raw[index] = next.Write(parameter.Type, values[index]); }
                }
                object? result = _method.ReturnType == typeof(void) ? null : next.Write(_method.Description.ReturnType, value);
                AbiMarshaller? previous = _snapshots.Value;
                _snapshots.Value = next;
                previous?.Dispose();
                return result;
            } catch (Exception exception) {
                next.Dispose();
                Console.Error.WriteLine($"Export {_contract}.{_method.Description.Name} : {exception}");
                for (int index = 0; index < raw.Length; index++) {
                    AbiParameterDescription parameter = _method.Description.Parameters[index];
                    if (parameter.Passing == AbiPassing.Out) { raw[index] = Activator.CreateInstance(AbiMarshaller.RawType(parameter.Type, parameter.Passing).GetElementType()!); }
                }
                Type type = AbiMarshaller.RawType(_method.Description.ReturnType, _method.Description.ReturnPassing);
                return type == typeof(void) ? null : Activator.CreateInstance(type);
            }
        }

        public void Dispose() {
            foreach (AbiMarshaller? snapshot in _snapshots.Values) { snapshot?.Dispose(); }
            _snapshots.Dispose();
            GC.KeepAlive(_callback);
        }
    }
}
