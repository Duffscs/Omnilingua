#pragma once

// Clang lit cette annotation pendant la génération ; MSVC n’en a pas besoin.
#define OMNILINGUA_DETAIL_STRINGIFY_INNER(value) #value
#define OMNILINGUA_DETAIL_STRINGIFY(value) OMNILINGUA_DETAIL_STRINGIFY_INNER(value)
#if defined(__clang__)
#define OMNILINGUA_PLUGIN_CONTRACT(name, version) \
    __attribute__((annotate("omnilingua.contract:" name ":" OMNILINGUA_DETAIL_STRINGIFY(version))))
#define OMNILINGUA_PLUGIN_EXPORT(interface) __attribute__((annotate("omnilingua.plugin.export:" #interface)))
#define OMNILINGUA_PLUGIN_IMPORT(interface, ...) __attribute__((annotate("omnilingua.plugin.import:" #interface ":" #__VA_ARGS__)))
#else
#define OMNILINGUA_PLUGIN_CONTRACT(name, version)
#define OMNILINGUA_PLUGIN_EXPORT(interface)
#define OMNILINGUA_PLUGIN_IMPORT(interface, ...)
#endif

namespace Omnilingua::Cpp {
    class IService { public: virtual ~IService() = default; };
    // Chaque spécialisation est produite dans le header .g.h du contrat local.
    template<class Interface> class ExportBinding;
}
