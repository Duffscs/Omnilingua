#pragma once
#include "Logger.h"
#include <Omnilingua/Plugin.h>

namespace LoggerCpp {
    class OMNILINGUA_PLUGIN_EXPORT(ILogger) Plugin final : public Omnilingua::Cpp::IPlugin {
        Logger _logger;
    public:
        std::vector<Omnilingua::Cpp::IService*> GetExport() override;
    };
}
