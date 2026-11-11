#pragma once
#include <Omnilingua/Export.h>
#include <string_view>

namespace HelloCpp {
    class ILogger : public virtual Omnilingua::Cpp::IService {
    public:
        virtual ~ILogger() = default;
        virtual void info(std::string_view context, std::string_view message) = 0;
    };
}
