using Omnilingua;
using Omnilingua.Dotnet;

namespace Dotnet.SourceGen.Tests;

public enum State : ulong { None = 0, Large = 1UL << 63 }
public sealed record Child {
    public string Name { get; set; } = "";
    public bool? Enabled { get; set; }
}
public sealed record Payload {
    public Child? Child { get; set; }
    public int[] Numbers { get; set; } = [];
    public List<string?> Names { get; set; } = [];
    public State State { get; set; }
    public int? Count { get; set; }
}

public interface IFixture : INotifications {
    Payload Echo(Payload value);
    List<Payload> EchoList(List<Payload> values);
    void Get(out Payload value, out ulong id);
    bool Fail(out Payload? value);
}

public interface INotifications { void Notify(string message); }

public sealed class Fixture : IFixture {
    public Payload? Last { get; private set; }
    public string? Message { get; private set; }
    public Payload Echo(Payload value) { Last = value; return value; }
    public List<Payload> EchoList(List<Payload> values) => values;
    public void Get(out Payload value, out ulong id) { value = Last!; id = ulong.MaxValue; }
    public bool Fail(out Payload? value) { value = null; throw new InvalidOperationException("Échec volontaire du fournisseur."); }
    public void Notify(string message) { Message = message; }
    private void Hidden() { }
}

[PluginExport(typeof(IFixture), Name = "IFixture", Version = 3)]
internal sealed partial class FixturePlugin(Fixture implementation, int exportCount = 1) : IPlugin {
    public void Init(PluginContext context) { }
    public object[] GetExport() => Enumerable.Repeat((object)implementation, exportCount).ToArray();
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public static PluginSettings Settings => new() { Name = "SourceGen.Tests", Type = "dotnet", Assembly = "test" };
}

internal sealed class EmptyPlugin : IPlugin { }
internal sealed class EmptyServices : IServiceProvider { public object? GetService(Type type) => null; }
