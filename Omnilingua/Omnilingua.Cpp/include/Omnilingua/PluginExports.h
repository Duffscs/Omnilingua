#pragma once
#include "CallingConvention.h"
#include "PluginMetadata.h"

#if defined(_WIN32)
#define PLUGIN_EXPORT extern "C" __declspec(dllexport)
#else
#define PLUGIN_EXPORT extern "C" __attribute__((visibility("default")))
#endif
