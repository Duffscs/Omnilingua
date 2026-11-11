#pragma once
#include <Omnilingua/Export.h>

namespace CodegenTests {
    class OMNILINGUA_PLUGIN_CONTRACT("IFlag", 1) IFlag : public virtual Omnilingua::Cpp::IService {
    public:
        virtual bool Enabled() = 0;
    };
}
