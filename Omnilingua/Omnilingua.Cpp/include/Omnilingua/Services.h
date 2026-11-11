#pragma once
#include "ExportSupport.h"
#include <functional>
#include <memory>
#include <typeindex>
#include <unordered_map>
#include <vector>

namespace Omnilingua::Cpp {
    template<class Interface> class ImportBinding;

    // Les objets et leur gestion mémoire restent dans le module consommateur.
    class ServiceProvider {
        struct Service {
            std::shared_ptr<void> Instance;
            std::function<bool()> Ready;
        };
        std::unordered_map<std::type_index, Service> _services;
    public:
        template<class Interface> void Add(const std::shared_ptr<ImportBinding<Interface>>& proxy) {
            std::shared_ptr<Interface> instance = proxy;
            if (!_services.emplace(typeid(Interface), Service{instance, [proxy] { return proxy->Ready(); }}).second) {
                throw std::logic_error("Interface importée plusieurs fois.");
            }
        }
        template<class Interface> Interface* GetService() const {
            auto found = _services.find(typeid(Interface));
            return found == _services.end() || !found->second.Ready() ? nullptr : static_cast<Interface*>(found->second.Instance.get());
        }
        template<class Interface> Interface& GetRequiredService() const {
            Interface* service = GetService<Interface>();
            if (!service) { throw std::logic_error("Service obligatoire absent ou incomplet."); }
            return *service;
        }
    };

    struct PluginContext {
        std::string_view Configuration;
        const ServiceProvider& Services;
    };

    class PluginBuilder {
        struct Binding {
            PluginContract Description;
            std::function<void*(const char*, const char*)> Export;
            std::function<bool(const char*, void*)> Inject;
            std::function<bool()> Ready;
            std::function<void()> Invalidate;
        };
        ServiceProvider _services;
        std::vector<Binding> _exports, _imports;
        std::vector<PluginContract> _exportDescriptions, _importDescriptions;
        PluginMetadata _metadata{};
        bool _built = false;
        std::vector<std::shared_ptr<void>> _exportOwners;

        void CheckDeclaration(const PluginContract& description, const std::vector<Binding>& bindings) const {
            if (_built) { throw std::logic_error("Déclarations déjà finalisées."); }
            for (const Binding& binding : bindings) {
                if (!std::strcmp(binding.Description.Name, description.Name)) { throw std::logic_error("Contrat dupliqué."); }
            }
        }
    public:
        PluginBuilder() = default;
        PluginBuilder(const PluginBuilder&) = delete;
        PluginBuilder& operator=(const PluginBuilder&) = delete;

        template<class Interface> void DeclareExport() {
            CheckDeclaration(ExportBinding<Interface>::Description(), _exports);
            _exports.push_back({ExportBinding<Interface>::Description(), {}, {}, {}, {}});
        }
        template<class Interface> void BindExport(const std::vector<IService*>& objects) {
            Interface* instance = nullptr;
            for (IService* object : objects) {
                if (Interface* match = dynamic_cast<Interface*>(object)) {
                    if (instance) { throw std::logic_error("Export ambigu : plusieurs objets implémentent le contrat."); }
                    instance = match;
                }
            }
            if (!instance) { throw std::logic_error("Objet exporté absent."); }
            auto owner = std::make_shared<ExportBinding<Interface>>(*instance);
            for (Binding& binding : _exports) {
                if (!std::strcmp(binding.Description.Name, ExportBinding<Interface>::Description().Name)) {
                    binding.Export = [owner](const char* contract, const char* method) { return owner->GetExport(contract, method); };
                    _exportOwners.push_back(owner);
                    return;
                }
            }
            throw std::logic_error("Contrat exporte non declare.");
        }
        void ResetExports() noexcept { for (Binding& binding : _exports) { binding.Export = {}; } _exportOwners.clear(); }

        template<class Interface> void Import(bool optional = false) {
            CheckDeclaration(ExportBinding<Interface>::Description(), _imports);
            auto proxy = std::make_shared<ImportBinding<Interface>>();
            _services.Add<Interface>(proxy);
            PluginContract description = ExportBinding<Interface>::Description();
            description.Flags = optional ? 1u : 0u;
            _imports.push_back({description, {}, [proxy](const char* method, void* pointer) { return proxy->Inject(method, pointer); },
                [proxy] { return proxy->Ready(); }, [proxy] { proxy->Invalidate(); }});
        }
        void Build() {
            if (_built) { throw std::logic_error("Déclarations déjà finalisées."); }
            for (const Binding& binding : _exports) { _exportDescriptions.push_back(binding.Description); }
            for (const Binding& binding : _imports) { _importDescriptions.push_back(binding.Description); }
            _metadata = {3, _importDescriptions.data(), static_cast<std::uint32_t>(_importDescriptions.size()),
                _exportDescriptions.data(), static_cast<std::uint32_t>(_exportDescriptions.size())};
            _built = true;
        }
        const PluginMetadata* Metadata() const noexcept { return &_metadata; }
        const ServiceProvider& Services() const noexcept { return _services; }
        void ValidateImports() const {
            for (const Binding& binding : _imports) {
                if (!(binding.Description.Flags & 1u) && !binding.Ready()) { throw std::logic_error("Import obligatoire absent ou incomplet."); }
            }
        }
        void* GetExport(const char* contract, const char* method) const noexcept {
            for (const Binding& binding : _exports) {
                if (void* pointer = binding.Export ? binding.Export(contract, method) : nullptr) { return pointer; }
            }
            return nullptr;
        }
        void Inject(const char* contract, const char* method, void* pointer) {
            if (!contract || !method || !pointer) { throw std::invalid_argument("Injection nulle."); }
            for (const Binding& binding : _imports) {
                if (!std::strcmp(contract, binding.Description.Name) && binding.Inject(method, pointer)) { return; }
            }
            throw std::invalid_argument("Import inconnu ou déjà injecté.");
        }
        void Invalidate() noexcept { for (const Binding& binding : _imports) { binding.Invalidate(); } }
    };
}
