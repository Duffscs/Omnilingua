#pragma once
#include "Contracts/ILogger.h"

namespace LoggerCpp {
    class Logger final : public ILogger {
    public:
        void debug(std::string_view context, std::string_view message) override;
        void info(std::string_view context, std::string_view message) override;
        void warn(std::string_view context, std::string_view message) override;
        void error(std::string_view context, std::string_view message) override;
        void fatal(std::string_view context, std::string_view message) override;
    private:
        static void Write(std::string_view level, std::string_view context, std::string_view message);
    };
}
