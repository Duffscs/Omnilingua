#pragma once
#include "PluginExports.h"
#include <array>
#include <cstddef>
#include <cstring>
#include <type_traits>

namespace Omnilingua::Cpp {
    // Types C++ utilisés pour vérifier les déclarations ; seule Description traverse l’ABI.
    template<class T> struct Type { AbiTypeDescription Description; };
    inline constexpr Type<std::int32_t> Int32{{AbiKind::Int32, "int32", 4, nullptr, 0, nullptr}};
    inline constexpr Type<std::uint32_t> UInt32{{AbiKind::UInt32, "uint32", 4, nullptr, 0, nullptr}};
    inline constexpr Type<std::uint64_t> UInt64{{AbiKind::UInt64, "uint64", 8, nullptr, 0, nullptr}};
    inline constexpr Type<std::int32_t> Boolean32{{AbiKind::Boolean32, "bool32", 4, nullptr, 0, nullptr}};
    inline constexpr Type<const char*> Utf8{{AbiKind::Utf8, "utf8", sizeof(void*), nullptr, 0, nullptr}};
    inline constexpr Type<void> Void{{AbiKind::Void, "void", 0, nullptr, 0, nullptr}};

    template<class Member, class T>
    constexpr AbiFieldDescription Field(const char* name, const Type<T>& type, std::size_t offset) {
        static_assert(std::is_same_v<Member, T>, "Le type du champ ne correspond pas au type ABI declare.");
        return {name, &type.Description, static_cast<std::uint32_t>(offset)};
    }

    template<class Mask>
    constexpr AbiFieldDescription Optional(AbiFieldDescription field, std::size_t offset, std::uint32_t mask) {
        static_assert(std::is_same_v<Mask, std::uint32_t>, "Le masque de presence doit etre uint32_t.");
        field.PresenceOffset = static_cast<std::int32_t>(offset);
        field.PresenceMask = mask;
        return field;
    }

    constexpr AbiFieldDescription Sentinel(AbiFieldDescription field, std::int64_t value) {
        field.HasNullSentinel = 1;
        field.NullSentinel = value;
        return field;
    }

    template<class Count>
    constexpr AbiFieldDescription Counted(AbiFieldDescription field, std::size_t countOffset) {
        static_assert(std::is_same_v<Count, std::uint32_t>, "Le compteur de tableau doit etre uint32_t.");
        field.CountOffset = static_cast<std::int32_t>(countOffset);
        return field;
    }

    template<class T, std::size_t N>
    constexpr Type<T> Record(const char* name, const AbiFieldDescription (&fields)[N]) {
        static_assert(std::is_standard_layout_v<T> && std::is_trivially_copyable_v<T>,
            "Un enregistrement ABI doit avoir une disposition standard et etre copiable trivialement.");
        return {{AbiKind::Record, name, sizeof(T), fields, N, nullptr}};
    }

    template<class T>
    constexpr Type<const T*> Array(const char* name, const Type<T>& element, std::uint32_t maxCount) {
        return {{AbiKind::Array, name, sizeof(void*), nullptr, 0, &element.Description, maxCount}};
    }

    template<class T, class Element, std::size_t N>
    constexpr Type<T> Sequence(const char* name, const Type<Element>& element,
        const AbiFieldDescription (&fields)[N], std::uint32_t maxCount) {
        static_assert(N == 1, "Une sequence decrit un champ tableau et son compteur.");
        Type<T> type = Record<T>(name, fields);
        type.Description.Kind = AbiKind::Sequence;
        type.Description.ElementType = &element.Description;
        type.Description.MaxCount = maxCount;
        return type;
    }

    template<class T> struct Binding {
        const char* Name;
        const AbiTypeDescription* Type;
        AbiPassing Passing;
    };
    template<class T> constexpr Binding<T> Value(const Type<T>& type) { return {nullptr, &type.Description, AbiPassing::Value}; }
    template<class T> constexpr Binding<const T*> Reference(const Type<T>& type) { return {nullptr, &type.Description, AbiPassing::Reference}; }
    template<class T> constexpr Binding<T> In(const char* name, const Type<T>& type) { return {name, &type.Description, AbiPassing::Value}; }
    template<class T> constexpr Binding<const T*> InRecord(const char* name, const Type<T>& type) { return {name, &type.Description, AbiPassing::Reference}; }
    template<class T> constexpr Binding<T*> Out(const char* name, const Type<T>& type) { return {name, &type.Description, AbiPassing::Out}; }
    template<class T> constexpr Binding<const T**> OutRecord(const char* name, const Type<T>& type) { return {name, &type.Description, AbiPassing::Out}; }

    // Stockage privé de construction ; ces objets C++ ne sont jamais transmis au Omnilingua.
    struct MethodEntry {
        const char* Name;
        void* Address;
        const AbiTypeDescription* ReturnType;
        AbiPassing ReturnPassing;
        std::array<AbiParameterDescription, 16> Parameters;
        std::uint32_t ParameterCount;
    };

    template<auto Function, class R, class... Args>
    MethodEntry Method(const char* name, Binding<R> result, Binding<Args>... parameters) {
        static_assert(sizeof...(Args) <= 16, "Une methode accepte au maximum 16 parametres.");
        static_assert(std::is_same_v<decltype(Function), R(OMNILINGUA_CALL*)(Args...) noexcept>,
            "La fonction doit etre Cdecl noexcept et correspondre exactement a la signature declaree.");
        return {name, reinterpret_cast<void*>(Function), result.Type, result.Passing,
            {{ {parameters.Name, parameters.Type, parameters.Passing}... }}, sizeof...(Args)};
    }

    template<std::size_t N> class Contract {
    public:
        template<class... Entries>
        Contract(const char* name, std::uint32_t version, Entries... entries)
            : _entries{entries...}, _description{name, _names.data(), N, version, _signatures.data(), N} {
            for (std::size_t i = 0; i < N; ++i) {
                const MethodEntry& entry = _entries[i];
                _names[i] = entry.Name;
                _signatures[i] = {entry.Name, entry.ReturnType, entry.ReturnPassing,
                    entry.ParameterCount ? entry.Parameters.data() : nullptr, entry.ParameterCount};
            }
        }
        // Les pointeurs internes restent valides pendant toute la vie du contrat.
        Contract(const Contract&) = delete;
        Contract& operator=(const Contract&) = delete;
        const PluginContract& Description() const noexcept { return _description; }
        void* GetExport(const char* contract, const char* method) const noexcept {
            if (!contract || !method || std::strcmp(contract, _description.Name)) { return nullptr; }
            for (const MethodEntry& entry : _entries) {
                if (!std::strcmp(method, entry.Name)) { return entry.Address; }
            }
            return nullptr;
        }
    private:
        std::array<MethodEntry, N> _entries;
        std::array<const char*, N> _names{};
        std::array<AbiMethodDescription, N> _signatures{};
        PluginContract _description;
    };
    template<class... Entries> Contract(const char*, std::uint32_t, Entries...) -> Contract<sizeof...(Entries)>;
}

// Les macros capturent les noms et offsetof ; les templates vérifient les types.
#define OMNILINGUA_FIELD(owner, member, type) \
    ::Omnilingua::Cpp::Field<decltype(owner::member)>(#member, type, offsetof(owner, member))
#define OMNILINGUA_OPTIONAL_FIELD(owner, member, type, presence, mask) \
    ::Omnilingua::Cpp::Optional<decltype(owner::presence)>(OMNILINGUA_FIELD(owner, member, type), offsetof(owner, presence), mask)
#define OMNILINGUA_SENTINEL_FIELD(owner, member, type, sentinel) \
    ::Omnilingua::Cpp::Sentinel(OMNILINGUA_FIELD(owner, member, type), sentinel)
#define OMNILINGUA_ARRAY_FIELD(owner, member, type, count) \
    ::Omnilingua::Cpp::Counted<decltype(owner::count)>(OMNILINGUA_FIELD(owner, member, type), offsetof(owner, count))
#define OMNILINGUA_RECORD(symbol, owner, name, ...) \
    constexpr AbiFieldDescription symbol##Fields[] = {__VA_ARGS__}; \
    constexpr auto symbol = ::Omnilingua::Cpp::Record<owner>(name, symbol##Fields)
#define OMNILINGUA_METHOD(function, ...) ::Omnilingua::Cpp::Method<&function>(#function, __VA_ARGS__)
#define OMNILINGUA_CONTRACT(symbol, name, version, ...) \
    const ::Omnilingua::Cpp::Contract symbol(name, version, __VA_ARGS__)
