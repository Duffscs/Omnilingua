#pragma once
#include <Omnilingua/Export.h>
#include <string_view>

namespace LoggerCpp {
    class ILogger : public virtual Omnilingua::Cpp::IService {
    public:
        virtual ~ILogger() = default;
        virtual void debug(std::string_view context, std::string_view message) = 0;
        virtual void info(std::string_view context, std::string_view message) = 0;
        virtual void warn(std::string_view context, std::string_view message) = 0;
        virtual void error(std::string_view context, std::string_view message) = 0;
        virtual void fatal(std::string_view context, std::string_view message) = 0;
    };
}
