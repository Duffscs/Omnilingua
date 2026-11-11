#pragma once
#include "Services.h"
#include "NativeApi.h"

namespace Omnilingua::Cpp {
    class IPlugin {
    public:
        virtual ~IPlugin() = default;
        virtual std::vector<IService*> GetExport() { return {}; }
        virtual void Init(const PluginContext&) {}
        virtual void Start() {}
        virtual void Stop() {}
    };

    // Les spécialisations sont générées depuis les annotations de la classe plugin.
    template<class Implementation> struct PluginDeclaration {
        static void Configure(PluginBuilder&) {}
        static void Bind(PluginBuilder&, const std::vector<IService*>& objects) {
            if (!objects.empty()) { throw std::logic_error("Objet exporté sans contrat annoncé."); }
        }
    };

    // L'instance métier est détruite après les adaptateurs qui la référencent.
    template<class Implementation> class PluginAdapter {
        static_assert(std::is_base_of_v<IPlugin, Implementation>);
        Implementation _plugin;
        PluginBuilder _builder;
        bool _initialized = false;
    public:
        PluginAdapter() { PluginDeclaration<Implementation>::Configure(_builder); _builder.Build(); }
        const PluginMetadata* Metadata() const noexcept { return _builder.Metadata(); }
        void* GetExport(const char* contract, const char* method) const noexcept { return _builder.GetExport(contract, method); }
        void Inject(const char* contract, const char* method, void* pointer) { _builder.Inject(contract, method, pointer); }
        void Init(const char* configuration) {
            _initialized = false;
            _builder.ResetExports();
            _builder.ValidateImports();
            _plugin.Init({BorrowedString(configuration), _builder.Services()});
            try { PluginDeclaration<Implementation>::Bind(_builder, _plugin.GetExport()); }
            catch (...) { _builder.ResetExports(); throw; }
            _initialized = true;
        }
        void Start() {
            if (!_initialized) { throw std::logic_error("Plugin non initialisé."); }
            _plugin.Start();
        }
        void Stop() {
            try { _plugin.Stop(); }
            catch (...) { _builder.Invalidate(); _initialized = false; throw; }
            _builder.Invalidate(); _initialized = false;
        }
    };

    template<class Implementation> class PluginEntry {
        using Adapter = PluginAdapter<Implementation>;
        static void* OMNILINGUA_CALL Create(const OmnilinguaPluginOptions* options) noexcept {
            return NativeCall([&]() -> void* { ValidateOptions(options); return new Adapter(); });
        }
        static const PluginMetadata* OMNILINGUA_CALL Metadata(void* handle) noexcept {
            return NativeCall([&] { return static_cast<Adapter*>(handle)->Metadata(); });
        }
        static std::int32_t OMNILINGUA_CALL Init(void* handle, const char* config) noexcept {
            return NativeCall([&] { static_cast<Adapter*>(handle)->Init(config); return 1; });
        }
        static std::int32_t OMNILINGUA_CALL Start(void* handle) noexcept {
            return NativeCall([&] { static_cast<Adapter*>(handle)->Start(); return 1; });
        }
        static std::int32_t OMNILINGUA_CALL Stop(void* handle) noexcept {
            return NativeCall([&] { static_cast<Adapter*>(handle)->Stop(); return 1; });
        }
        static void* OMNILINGUA_CALL Export(void* handle, const char* contract, const char* method) noexcept {
            return NativeCall([&] { return static_cast<Adapter*>(handle)->GetExport(contract, method); });
        }
        static std::int32_t OMNILINGUA_CALL Inject(void* handle, const char* contract, const char* method, void* pointer) noexcept {
            return NativeCall([&] { static_cast<Adapter*>(handle)->Inject(contract, method, pointer); return 1; });
        }
        static std::int32_t OMNILINGUA_CALL Destroy(void* handle) noexcept {
            return NativeCall([&] { delete static_cast<Adapter*>(handle); return 1; });
        }
        static const char* OMNILINGUA_CALL Error(void*) noexcept { return NativeError.c_str(); }
    public:
        static const OmnilinguaPluginApi* Api() noexcept {
            static const OmnilinguaPluginApi api{0, 0,
                Create, Metadata, Init, Start, Stop, Export, Inject, Destroy, Error, nullptr, nullptr};
            return &api;
        }
    };
}

#define OMNILINGUA_PLUGIN(implementation) \
    PLUGIN_EXPORT const OmnilinguaPluginApi* OMNILINGUA_CALL OmnilinguaPlugin_GetApi() noexcept { \
        return ::Omnilingua::Cpp::PluginEntry<implementation>::Api(); }
