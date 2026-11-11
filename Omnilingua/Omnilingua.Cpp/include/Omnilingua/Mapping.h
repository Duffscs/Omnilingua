#pragma once
#include "TextEncoding.h"
#include <cstring>
#include <memory>
#include <optional>
#include <tuple>
#include <type_traits>
#include <utility>
#include <vector>

namespace Omnilingua::Cpp {
    // Possède uniquement les buffers de l’adaptateur ; aucun allocateur ne traverse l’ABI.
    class BufferStorage {
    public:
        static constexpr std::size_t MaxStringBytes = 131072;
        template<class T> T* Array(std::size_t count) {
            if (!count) { return nullptr; }
            std::unique_ptr<T[]> values = std::make_unique<T[]>(count);
            T* pointer = values.get();
            std::shared_ptr<void> owner(values.release(), [](void* p) { delete[] static_cast<T*>(p); });
            _owners.push_back(std::move(owner));
            return pointer;
        }
        const char* String(const std::string& text) {
            if (text.size() > MaxStringBytes || text.find('\0') != std::string::npos) { throw std::invalid_argument("Chaine ABI invalide ou trop longue."); }
            char* buffer = Array<char>(text.size() + 1);
            std::memcpy(buffer, text.c_str(), text.size() + 1);
            return buffer;
        }
    private:
        std::vector<std::shared_ptr<void>> _owners;
    };

    inline std::string_view BorrowedString(const char* text) {
        if (!text) { return {}; }
        std::size_t count = 0;
        while (count <= BufferStorage::MaxStringBytes && text[count]) { ++count; }
        if (count > BufferStorage::MaxStringBytes) { throw std::invalid_argument("Chaine ABI trop longue."); }
        return {text, count};
    }

    template<class Target, class Source> Target ReadValue(const Source& source) {
        if constexpr (std::is_same_v<Target, std::wstring> && std::is_same_v<Source, const char*>) { return FromUtf8(BorrowedString(source)); }
        else if constexpr (std::is_same_v<Target, std::string> && std::is_same_v<Source, const char*>) {
            std::string_view text = BorrowedString(source);
            FromUtf8(text);
            return std::string(text);
        } else if constexpr (std::is_same_v<Target, bool> && std::is_same_v<Source, std::int32_t>) {
            if (source != 0 && source != 1) { throw std::invalid_argument("Booleen ABI invalide."); }
            return source != 0;
        } else {
            static_assert(std::is_same_v<Target, Source> && std::is_arithmetic_v<Target>, "Conversion de champ non prise en charge.");
            return source;
        }
    }

    template<class Target, class Source> Target WriteValue(const Source& source, BufferStorage& storage) {
        if constexpr (std::is_same_v<Target, const char*> && std::is_same_v<Source, std::wstring>) { return storage.String(ToUtf8(source)); }
        else if constexpr (std::is_same_v<Target, const char*> && std::is_same_v<Source, std::string>) { FromUtf8(source); return storage.String(source); }
        else if constexpr (std::is_same_v<Target, std::int32_t> && std::is_same_v<Source, bool>) { return source ? 1 : 0; }
        else {
            static_assert(std::is_same_v<Target, Source> && std::is_arithmetic_v<Target>, "Conversion de champ non prise en charge.");
            return source;
        }
    }

    template<auto First, auto... Rest, class T> decltype(auto) Member(T& object) {
        if constexpr (sizeof...(Rest) == 0) { return (object.*First); }
        else { return Member<Rest...>(object.*First); }
    }
    template<auto... Members> struct Path {
        template<class T> decltype(auto) operator()(T& object) const { return Member<Members...>(object); }
    };

    template<auto AbiMember, class Access> struct FieldMapping {
        Access Get;
        template<class M, class A> void Read(M& model, const A& abi) const {
            using Value = std::remove_cvref_t<decltype(Get(model))>;
            Get(model) = ReadValue<Value>(abi.*AbiMember);
        }
        template<class M, class A> void Write(const M& model, A& abi, BufferStorage& storage) const {
            using Value = std::remove_cvref_t<decltype(abi.*AbiMember)>;
            abi.*AbiMember = WriteValue<Value>(Get(model), storage);
        }
    };
    template<auto Member, class Access> constexpr auto MapField(Access access) { return FieldMapping<Member, Access>{access}; }

    template<auto AbiMember, auto MaskMember, class Access> struct OptionalMapping {
        Access Get;
        std::uint32_t Bit;
        template<class M, class A> void Read(M& model, const A& abi) const {
            using Value = typename std::remove_cvref_t<decltype(Get(model))>::value_type;
            if (abi.*MaskMember & Bit) { Get(model) = ReadValue<Value>(abi.*AbiMember); }
            else { Get(model).reset(); }
        }
        template<class M, class A> void Write(const M& model, A& abi, BufferStorage& storage) const {
            using Value = std::remove_cvref_t<decltype(abi.*AbiMember)>;
            if (Get(model)) { abi.*MaskMember |= Bit; abi.*AbiMember = WriteValue<Value>(*Get(model), storage); }
            else { abi.*MaskMember &= ~Bit; }
        }
    };
    template<auto Member, auto Mask, class Access> constexpr auto MapOptional(Access access, std::uint32_t bit) {
        if (!bit || (bit & (bit - 1))) { throw std::invalid_argument("Un seul bit de presence est requis."); }
        return OptionalMapping<Member, Mask, Access>{access, bit};
    }

    template<auto AbiMember, class Access> struct NullableMapping {
        Access Get;
        std::int32_t Sentinel;
        template<class M, class A> void Read(M& model, const A& abi) const {
            using Value = typename std::remove_cvref_t<decltype(Get(model))>::value_type;
            if (abi.*AbiMember == Sentinel) { Get(model).reset(); }
            else { Get(model) = ReadValue<Value>(abi.*AbiMember); }
        }
        template<class M, class A> void Write(const M& model, A& abi, BufferStorage& storage) const {
            using Value = std::remove_cvref_t<decltype(abi.*AbiMember)>;
            abi.*AbiMember = Get(model) ? WriteValue<Value>(*Get(model), storage) : Sentinel;
        }
    };
    template<auto Member, class Access> constexpr auto MapNullable(Access access, std::int32_t sentinel = -1) {
        return NullableMapping<Member, Access>{access, sentinel};
    }

    template<class Model, class Abi, class... Fields> struct RecordMapping {
        using ModelType = Model;
        using AbiType = Abi;
        std::tuple<Fields...> FieldsMap;
        Model Read(const Abi* abi) const {
            if (!abi) { throw std::invalid_argument("Objet ABI nul."); }
            Model model{};
            std::apply([&](const auto&... field) { (field.Read(model, *abi), ...); }, FieldsMap);
            return model;
        }
        Abi Write(const Model& model, BufferStorage& storage) const {
            Abi abi{};
            std::apply([&](const auto&... field) { (field.Write(model, abi, storage), ...); }, FieldsMap);
            return abi;
        }
    };
    template<class Model, class Abi, class... Fields> constexpr auto MapRecord(Fields... fields) {
        static_assert(std::is_standard_layout_v<Abi> && std::is_trivially_copyable_v<Abi>);
        return RecordMapping<Model, Abi, Fields...>{{fields...}};
    }

    template<auto Items, auto Count, class Access, class ElementMap> struct ArrayMapping {
        Access Get;
        ElementMap Element;
        std::uint32_t MaxCount;
        template<class M, class A> void Read(M& model, const A& abi) const {
            static_assert(std::is_same_v<std::remove_cvref_t<decltype(abi.*Count)>, std::uint32_t>, "Le compteur ABI doit etre uint32_t.");
            const auto count = abi.*Count;
            if (count > MaxCount || (count && !(abi.*Items))) { throw std::invalid_argument("Tableau ABI invalide."); }
            auto& target = Get(model);
            target.clear();
            target.reserve(count);
            for (std::uint32_t i = 0; i < count; ++i) { target.push_back(Element.Read((abi.*Items) + i)); }
        }
        template<class M, class A> void Write(const M& model, A& abi, BufferStorage& storage) const {
            static_assert(std::is_same_v<std::remove_cvref_t<decltype(abi.*Count)>, std::uint32_t>, "Le compteur ABI doit etre uint32_t.");
            const auto& source = Get(model);
            if (source.size() > MaxCount) { throw std::invalid_argument("Tableau ABI trop grand."); }
            auto* buffer = storage.Array<typename ElementMap::AbiType>(source.size());
            for (std::size_t i = 0; i < source.size(); ++i) { buffer[i] = Element.Write(source[i], storage); }
            abi.*Items = buffer;
            abi.*Count = static_cast<std::uint32_t>(source.size());
        }
    };
    template<auto Items, auto Count, class Access, class ElementMap>
    constexpr auto MapArray(Access access, ElementMap element, std::uint32_t maxCount) {
        if (!maxCount || maxCount > 65536) { throw std::invalid_argument("Limite de tableau ABI invalide."); }
        return ArrayMapping<Items, Count, Access, ElementMap>{access, element, maxCount};
    }
    struct Identity { template<class T> T& operator()(T& value) const { return value; } };

    template<class Mapping, class Validator> struct ValidatedMapping {
        using ModelType = typename Mapping::ModelType;
        using AbiType = typename Mapping::AbiType;
        Mapping Inner;
        Validator Validate;
        ModelType Read(const AbiType* abi) const {
            if (!abi) { throw std::invalid_argument("Objet ABI nul."); }
            Validate(*abi);
            return Inner.Read(abi);
        }
        AbiType Write(const ModelType& model, BufferStorage& storage) const {
            AbiType abi = Inner.Write(model, storage);
            Validate(abi);
            return abi;
        }
    };
    template<class Mapping, class Validator> constexpr auto ValidateMapping(Mapping mapping, Validator validate) {
        return ValidatedMapping<Mapping, Validator>{mapping, validate};
    }

    // Le résultat est emprunté jusqu’au prochain Set réussi sur ce même snapshot.
    // Un échec de conversion conserve l’ancien résultat et libère les allocations partielles.
    template<class Mapping> class Snapshot {
    public:
        explicit Snapshot(Mapping mapping) : _mapping(std::move(mapping)) {}
        const typename Mapping::AbiType* Set(const typename Mapping::ModelType& model) {
            auto next = std::make_unique<State>();
            next->Value = _mapping.Write(model, next->Storage);
            _state = std::move(next);
            return &_state->Value;
        }
    private:
        struct State { BufferStorage Storage; typename Mapping::AbiType Value{}; };
        Mapping _mapping;
        std::unique_ptr<State> _state;
    };
}
