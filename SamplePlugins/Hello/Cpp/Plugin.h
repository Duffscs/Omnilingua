#pragma once
#include "Contracts/ILogger.h"
#include <Omnilingua/Plugin.h>

namespace HelloCpp {
    class OMNILINGUA_PLUGIN_IMPORT(ILogger) Plugin final : public Omnilingua::Cpp::IPlugin {
        ILogger* _logger = nullptr;
    public:
        void Init(const Omnilingua::Cpp::PluginContext& context) override;
        void Start() override;
    };
}
