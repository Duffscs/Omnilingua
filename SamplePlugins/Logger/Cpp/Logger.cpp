#include "Logger.h"
#include <iostream>
#include <mutex>

namespace LoggerCpp {
    void Logger::Write(std::string_view level, std::string_view context, std::string_view message) {
        static std::mutex gate;
        std::lock_guard<std::mutex> lock(gate);
        std::cout << "[LoggerCPP] [" << level << "] [" << context << "] " << message << std::endl;
    }
    void Logger::debug(std::string_view context, std::string_view message) { Write("DEBUG", context, message); }
    void Logger::info(std::string_view context, std::string_view message) { Write("INFO", context, message); }
    void Logger::warn(std::string_view context, std::string_view message) { Write("WARN", context, message); }
    void Logger::error(std::string_view context, std::string_view message) { Write("ERROR", context, message); }
    void Logger::fatal(std::string_view context, std::string_view message) { Write("FATAL", context, message); }
}
