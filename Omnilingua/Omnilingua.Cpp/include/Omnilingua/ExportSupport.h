#pragma once
#include "Export.h"
#include "Declaration.h"
#include "Mapping.h"
#include <atomic>
#include <array>
#include <utility>
#include <cstdio>
#include <stdexcept>
#include <string_view>
#include <type_traits>

namespace Omnilingua::Cpp {
    // Les trampolines générés interceptent les exceptions avant la frontière C ABI.
    inline void ReportExportError(const char* contract, const char* method, const char* message) noexcept {
        std::fprintf(stderr, "[Omnilingua.Cpp] %s.%s : %s\n", contract, method, message);
    }
}
