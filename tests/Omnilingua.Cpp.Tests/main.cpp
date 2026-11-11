#include <Omnilingua/Declaration.h>
#include <iostream>
#include <stdexcept>

using namespace Omnilingua::Cpp;

static_assert(sizeof(AbiTypeDescription) == 56);
static_assert(sizeof(AbiFieldDescription) == 48);
static_assert(sizeof(AbiParameterDescription) == 24);
static_assert(sizeof(AbiMethodDescription) == 40);
static_assert(sizeof(PluginContract) == 40);
static_assert(sizeof(PluginMetadata) == 40);

namespace {
    struct Item { std::uint64_t Id; std::int32_t X; std::uint32_t Mask; std::int32_t Visible; };
    OMNILINGUA_RECORD(ItemType, Item, "Item",
        OMNILINGUA_FIELD(Item, Id, UInt64),
        OMNILINGUA_OPTIONAL_FIELD(Item, X, Int32, Mask, 1),
        OMNILINGUA_SENTINEL_FIELD(Item, Visible, Boolean32, -1)
    );
    static_assert(ItemType.Description.Size == sizeof(Item));
    static_assert(ItemTypeFields[1].PresenceOffset == offsetof(Item, Mask));
    static_assert(ItemTypeFields[2].HasNullSentinel == 1);
    std::int32_t OMNILINGUA_CALL Add(const Item* item, std::uint64_t* id) noexcept { *id = item->Id; return 1; }
    const Item* OMNILINGUA_CALL Get() noexcept { static const Item item{42}; return &item; }

    OMNILINGUA_CONTRACT(Exports, "ITest", 7,
        OMNILINGUA_METHOD(Add, Value(Int32), InRecord("item", ItemType), Out("id", UInt64)),
        OMNILINGUA_METHOD(Get, Reference(ItemType))
    );

    // Ces modes servent à vérifier que des déclarations erronées refusent de compiler.
#if defined(OMNILINGUA_TEST_BAD_SIGNATURE)
    const auto InvalidMethod = OMNILINGUA_METHOD(Add, Value(UInt32), InRecord("item", ItemType), Out("id", UInt64));
#elif defined(OMNILINGUA_TEST_BAD_FIELD)
    constexpr auto InvalidField = OMNILINGUA_FIELD(Item, Id, UInt32);
#endif

    void Check(bool condition) { if (!condition) { throw std::runtime_error("Declaration ABI incorrecte."); } }
}

void TestMappings();

int main() {
    TestMappings();
    const PluginContract& description = Exports.Description();
    Check(description.Version == 7 && description.MethodCount == 2 && description.SignatureCount == 2);
    Check(!std::strcmp(description.Methods[0], description.Signatures[0].Name));
    Check(description.Signatures[0].Parameters[1].Passing == AbiPassing::Out);
    Check(description.Signatures[1].ParameterCount == 0 && !description.Signatures[1].Parameters);
    Check(!Exports.GetExport(nullptr, "Add") && !Exports.GetExport("ITest", nullptr));
    Check(!Exports.GetExport("Autre", "Add") && !Exports.GetExport("ITest", "Absent"));
    auto add = reinterpret_cast<decltype(&Add)>(Exports.GetExport("ITest", "Add"));
    auto get = reinterpret_cast<decltype(&Get)>(Exports.GetExport("ITest", "Get"));
    std::uint64_t id = 0;
    Check(add(get(), &id) == 1 && id == 42);
    std::cout << "PASS: table commune, signatures, champs optionnels et appels C ABI.\n";
}
