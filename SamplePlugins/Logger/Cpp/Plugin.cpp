#include "Plugin.g.h"

namespace LoggerCpp {
    std::vector<Omnilingua::Cpp::IService*> Plugin::GetExport() {
        return {&_logger};
    }
}

OMNILINGUA_PLUGIN(LoggerCpp::Plugin)
