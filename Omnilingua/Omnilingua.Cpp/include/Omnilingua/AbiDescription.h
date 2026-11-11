#pragma once
#include <cstdint>

// Descriptions C ABI génériques : aucun objet C++ ni chaîne allouée ne traverse le protocole.
enum class AbiKind : std::uint32_t { Void = 0, Int32 = 1, UInt32 = 2, UInt64 = 3, Boolean32 = 4, Utf8 = 5, Record = 6, Array = 7, Sequence = 8 };
enum class AbiPassing : std::uint32_t { Value, Reference, Out };
struct AbiFieldDescription;
struct AbiTypeDescription {
    AbiKind Kind;
    const char* Name;
    std::uint32_t Size;
    const AbiFieldDescription* Fields;
    std::uint32_t FieldCount;
    const AbiTypeDescription* ElementType;
    std::uint32_t MaxCount = 256;
};
struct AbiFieldDescription {
    const char* Name;
    const AbiTypeDescription* Type;
    std::uint32_t Offset;
    std::int32_t PresenceOffset = -1;
    std::uint32_t PresenceMask = 0;
    std::uint32_t HasNullSentinel = 0;
    std::int64_t NullSentinel = -1;
    std::int32_t CountOffset = -1;
};
struct AbiParameterDescription {
    const char* Name;
    const AbiTypeDescription* Type;
    AbiPassing Passing;
};
struct AbiMethodDescription {
    const char* Name;
    const AbiTypeDescription* ReturnType;
    AbiPassing ReturnPassing;
    const AbiParameterDescription* Parameters;
    std::uint32_t ParameterCount;
};
