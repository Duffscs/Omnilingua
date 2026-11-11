#pragma once
#include <cstdint>
#include "AbiDescription.h"

struct PluginContract {
    const char* Name;
    const char** Methods;
    std::uint32_t MethodCount;
    std::uint32_t Version = 1;
    const AbiMethodDescription* Signatures = nullptr;
    std::uint32_t SignatureCount = 0;
    std::uint32_t Flags = 0; // Bit 0 : dépendance optionnelle (protocole version 3).
};
