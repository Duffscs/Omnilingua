#include <Omnilingua/Plugin.h>
#include <iostream>

class Plugin final : public Omnilingua::Cpp::IPlugin {
public:
    void Start() override {
        std::cout << "[Premier.Cpp] Le plugin a démarré !" << std::endl;
    }
};

OMNILINGUA_PLUGIN(Plugin)
