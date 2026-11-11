#pragma once
#include "PluginContract.h"

struct PluginMetadata {
    std::uint32_t ProtocolVersion;
    const PluginContract* Dependencies;
    std::uint32_t DependencyCount;
    const PluginContract* Exports;
    std::uint32_t ExportCount;
};
