#include "Plugins.g.h"
#include <Omnilingua/Plugin.h>
#include <iostream>
#include <stdexcept>

namespace {
    class Service final : public CodegenTests::ITest {
    public:
        int Add(int left, int right) override {
            if (left < 0) { throw std::invalid_argument("Erreur de test"); }
            return left + right;
        }
        bool IsEnabled() const noexcept override { return _enabled; }
        void SetEnabled(bool value) override { _enabled = value; }
        unsigned int Size(unsigned int value) override { return value; }
        unsigned long long Id(unsigned long long value) override { return value; }
        std::uint64_t EchoId(std::uint64_t value) override { return value; }
    private:
        bool _enabled = false;
    };
    void Check(bool condition) { if (!condition) { throw std::runtime_error("Export genere incorrect."); } }
}

int main() {
    const std::string unicode = "été 漢字 🙂";
    Check(Omnilingua::Cpp::ToUtf8(Omnilingua::Cpp::FromUtf16(Omnilingua::Cpp::ToUtf16(Omnilingua::Cpp::FromUtf8(unicode)))) == unicode);
    {
        Omnilingua::Cpp::PluginAdapter<CodegenTests::Provider> provider;
        Check(provider.Metadata()->ExportCount == 1 && provider.Metadata()->Exports[0].Version == 2);
        provider.Init("{}");
        Omnilingua::Cpp::PluginAdapter<CodegenTests::Consumer> consumer;
        bool missing = false;
        try { consumer.Init("{}"); } catch (const std::logic_error&) { missing = true; }
        Check(missing && consumer.Metadata()->DependencyCount == 1);
        const PluginContract& contract = provider.Metadata()->Exports[0];
        for (std::uint32_t index = 0; index < contract.MethodCount; ++index) {
            consumer.Inject(contract.Name, contract.Methods[index], provider.GetExport(contract.Name, contract.Methods[index]));
        }
        consumer.Init("{}"); consumer.Start(); consumer.Stop(); provider.Stop();
    }
    for (int count : {0, 2, 3, 4}) {
        CodegenTests::Provider::ObjectCount = count;
        Omnilingua::Cpp::PluginAdapter<CodegenTests::Provider> invalid;
        bool rejected = false;
        try { invalid.Init("{}"); } catch (const std::logic_error&) { rejected = true; }
        Check(rejected);
    }
    CodegenTests::Provider::ObjectCount = 1;
    {
        Omnilingua::Cpp::PluginAdapter<CodegenTests::MultiProvider> multi;
        Check(multi.Metadata()->ExportCount == 2);
        multi.Init("{}");
        auto enabled = reinterpret_cast<std::int32_t(OMNILINGUA_CALL*)()>(multi.GetExport("IFlag", "Enabled"));
        Check(enabled() == 1);
        multi.Stop();
    }
    Omnilingua::Cpp::PluginAdapter<CodegenTests::OptionalConsumer> optionalPlugin;
    Check(optionalPlugin.Metadata()->Dependencies[0].Flags == 1);
    optionalPlugin.Init("{}"); optionalPlugin.Start(); optionalPlugin.Stop();
    Service service;
    Omnilingua::Cpp::ExportBinding<CodegenTests::ITest> binding(service);
    const PluginMetadata* metadata = binding.Metadata();
    Check(metadata->ProtocolVersion == 3 && metadata->ExportCount == 1);
    Check(metadata->Exports[0].Version == 2 && metadata->Exports[0].SignatureCount == 6);
    Check(!binding.GetExport("Absent", "Add") && !binding.GetExport("ITest", "Absent"));
    auto add = reinterpret_cast<std::int32_t(OMNILINGUA_CALL*)(std::int32_t, std::int32_t)>(binding.GetExport("ITest", "Add"));
    auto get = reinterpret_cast<std::int32_t(OMNILINGUA_CALL*)()>(binding.GetExport("ITest", "IsEnabled"));
    auto set = reinterpret_cast<void(OMNILINGUA_CALL*)(std::int32_t)>(binding.GetExport("ITest", "SetEnabled"));
    auto size = reinterpret_cast<std::uint32_t(OMNILINGUA_CALL*)(std::uint32_t)>(binding.GetExport("ITest", "Size"));
    auto id = reinterpret_cast<std::uint64_t(OMNILINGUA_CALL*)(std::uint64_t)>(binding.GetExport("ITest", "Id"));
    Check(size(4000000000u) == 4000000000u && id(9000000000000000000ull) == 9000000000000000000ull);
    Check(add(2, 3) == 5 && get() == 0);
    set(1); Check(get() == 1);
    set(2); Check(get() == 1); // Le booléen invalide est rejeté avant l’appel métier.
    Check(add(-1, 3) == 0 && add(4, 5) == 9); // L’exception ne traverse pas l’ABI.
    Service second;
    Omnilingua::Cpp::ExportBinding<CodegenTests::ITest> independent(second);
    auto secondGet = reinterpret_cast<std::int32_t(OMNILINGUA_CALL*)()>(independent.GetExport("ITest", "IsEnabled"));
    Check(secondGet() == 0 && get() == 1 && add(1, 1) == 2);
    Omnilingua::Cpp::PluginBuilder consumer;
    consumer.Import<CodegenTests::ITest>();
    consumer.Build();
    Check(consumer.Metadata()->DependencyCount == 1 && consumer.Metadata()->ExportCount == 0);
    Check(consumer.Services().GetService<CodegenTests::ITest>() == nullptr);
    bool missingRejected = false;
    try { consumer.ValidateImports(); } catch (const std::logic_error&) { missingRejected = true; }
    Check(missingRejected);
    for (std::uint32_t index = 0; index < metadata->Exports[0].MethodCount; ++index) {
        const char* method = metadata->Exports[0].Methods[index];
        consumer.Inject("ITest", method, binding.GetExport("ITest", method));
    }
    consumer.ValidateImports();
    CodegenTests::ITest& imported = consumer.Services().GetRequiredService<CodegenTests::ITest>();
    Check(imported.Add(20, 22) == 42 && imported.Id(9000000000000000000ull) == 9000000000000000000ull);
    Check(imported.Size(4000000000u) == 4000000000u);
    Check(imported.EchoId(9000000000000000000ull) == 9000000000000000000ull);
    imported.SetEnabled(true); Check(imported.IsEnabled());
    imported.SetEnabled(false); Check(!imported.IsEnabled());
    bool reinjectionRejected = false;
    try { consumer.Inject("ITest", "Add", binding.GetExport("ITest", "Add")); }
    catch (const std::invalid_argument&) { reinjectionRejected = true; }
    Check(reinjectionRejected && imported.Add(1, 2) == 3);
    consumer.Invalidate();
    Check(consumer.Services().GetService<CodegenTests::ITest>() == nullptr);
    bool closedRejected = false;
    try { imported.Add(1, 2); } catch (const std::logic_error&) { closedRejected = true; }
    Check(closedRejected);
    Omnilingua::Cpp::PluginBuilder optional;
    optional.Import<CodegenTests::ITest>(true);
    optional.Build();
    optional.ValidateImports();
    Check(optional.Metadata()->Dependencies[0].Flags == 1 && optional.Services().GetService<CodegenTests::ITest>() == nullptr);
    Omnilingua::Cpp::PluginAdapter<Omnilingua::Cpp::IPlugin> empty;
    empty.Init("{}"); empty.Start(); empty.Stop();
    Check(empty.Metadata()->DependencyCount == 0 && empty.Metadata()->ExportCount == 0);
    std::cout << "PASS: contrat genere independant du logger, scalaires, booleens, exceptions et instances independantes.\n";
    std::cout << "PASS: classes injectees, imports obligatoires/optionnels, invalidation et cycle de vie par defaut.\n";
}
