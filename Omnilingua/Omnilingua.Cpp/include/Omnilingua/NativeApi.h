#pragma once
#include "PluginExports.h"
#include <cstddef>
#include <stdexcept>
#include <string>

struct OmnilinguaPluginOptions {
    const char* RuntimeLibrary;
    const char* RuntimeHome;
    const char* RuntimeExecutable;
    const char* Sdk;
    const char* Assembly;
    const char* EntryPoint;
};

enum OmnilinguaPluginCapabilities : std::uint32_t {
    OmnilinguaPluginDynamicCalls = 1,
    OmnilinguaPluginCallbackImports = 2,
    OmnilinguaPluginProcessLifetime = 4
};

struct OmnilinguaPluginApi {
    std::uint32_t Capabilities;
    std::uint32_t RuntimeVersion;
    void* (OMNILINGUA_CALL* Create)(const OmnilinguaPluginOptions*) noexcept;
    const PluginMetadata* (OMNILINGUA_CALL* GetMetadata)(void*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Init)(void*, const char*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Start)(void*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Stop)(void*) noexcept;
    void* (OMNILINGUA_CALL* GetExport)(void*, const char*, const char*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Inject)(void*, const char*, const char*, void*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Destroy)(void*) noexcept;
    const char* (OMNILINGUA_CALL* GetError)(void*) noexcept;
    std::int32_t (OMNILINGUA_CALL* Invoke)(void*, const char*, const char*, const char**, std::int32_t) noexcept;
    std::int32_t (OMNILINGUA_CALL* BindImports)(void*, const char*, void*) noexcept;
};

namespace Omnilingua::Cpp {
    inline void ValidateOptions(const OmnilinguaPluginOptions* options) {
        if (!options) {
            throw std::invalid_argument("Options de creation natives incompatibles.");
        }
    }
    inline thread_local std::string NativeError;
    template<class Function> auto NativeCall(Function function) noexcept -> decltype(function()) {
        try { NativeError.clear(); return function(); }
        catch (const std::exception& error) { NativeError = error.what(); }
        catch (...) { NativeError = "Exception native inconnue."; }
        return {};
    }
}

PLUGIN_EXPORT const OmnilinguaPluginApi* OMNILINGUA_CALL OmnilinguaPlugin_GetApi() noexcept;
