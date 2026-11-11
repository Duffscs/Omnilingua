#pragma once
#include "Contracts/ITest.h"
#include "Contracts/IFlag.h"
#include <Omnilingua/Plugin.h>

namespace CodegenTests {
    class TestService final : public ITest {
        bool _enabled = false;
    public:
        int Add(int left, int right) override { return left + right; }
        bool IsEnabled() const noexcept override { return _enabled; }
        void SetEnabled(bool value) override { _enabled = value; }
        unsigned int Size(unsigned int value) override { return value; }
        unsigned long long Id(unsigned long long value) override { return value; }
        std::uint64_t EchoId(std::uint64_t value) override { return value; }
    };

    class OMNILINGUA_PLUGIN_EXPORT(ITest) Provider final : public Omnilingua::Cpp::IPlugin {
        TestService _service;
        Omnilingua::Cpp::IService _undeclared;
    public:
        inline static int ObjectCount = 1;
        std::vector<Omnilingua::Cpp::IService*> GetExport() override {
            if (ObjectCount == 0) { return {}; }
            if (ObjectCount == 2) { return {&_service, &_service}; }
            if (ObjectCount == 3) { return {&_undeclared}; }
            if (ObjectCount == 4) { return {nullptr}; }
            return {&_service};
        }
    };

    class OMNILINGUA_PLUGIN_IMPORT(ITest) Consumer final : public Omnilingua::Cpp::IPlugin {
        ITest* _service = nullptr;
    public:
        void Init(const Omnilingua::Cpp::PluginContext& context) override { _service = &context.Services.GetRequiredService<ITest>(); }
        void Start() override {
            if (_service->Add(20, 22) != 42) { throw std::logic_error("Service injecté incorrect."); }
        }
    };

    class OMNILINGUA_PLUGIN_IMPORT(ITest, true) OptionalConsumer final : public Omnilingua::Cpp::IPlugin {
    public:
        void Init(const Omnilingua::Cpp::PluginContext& context) override {
            if (context.Services.GetService<ITest>()) { throw std::logic_error("Service optionnel inattendu."); }
        }
    };

    class MultiService final : public ITest, public IFlag {
        TestService _service;
    public:
        bool Enabled() override { return true; }
        int Add(int left, int right) override { return _service.Add(left, right); }
        bool IsEnabled() const noexcept override { return _service.IsEnabled(); }
        void SetEnabled(bool value) override { _service.SetEnabled(value); }
        unsigned int Size(unsigned int value) override { return value; }
        unsigned long long Id(unsigned long long value) override { return value; }
        std::uint64_t EchoId(std::uint64_t value) override { return value; }
    };

    class OMNILINGUA_PLUGIN_EXPORT(ITest) OMNILINGUA_PLUGIN_EXPORT(IFlag) MultiProvider final : public Omnilingua::Cpp::IPlugin {
        MultiService _service;
    public:
        std::vector<Omnilingua::Cpp::IService*> GetExport() override { return {&_service}; }
    };
}
