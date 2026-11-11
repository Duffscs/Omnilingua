#include "Plugin.g.h"

namespace HelloCpp {
    void Plugin::Init(const Omnilingua::Cpp::PluginContext& context) {
        _logger = &context.Services.GetRequiredService<ILogger>();
    }

    void Plugin::Start() {
        _logger->info("Hello.Cpp", "Bonjour depuis le plugin C++ ! été🙂");
    }
}

OMNILINGUA_PLUGIN(HelloCpp::Plugin)
