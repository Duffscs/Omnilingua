#include <Omnilingua/Mapping.h>
#include <iostream>
#include <stdexcept>

using namespace Omnilingua::Cpp;

namespace {
    struct Model {
        std::wstring Text;
        std::optional<std::int32_t> X;
        std::optional<bool> Visible;
    };
    struct Abi { const char* Text; std::int32_t X; std::uint32_t Mask; std::int32_t Visible; };
    struct ListAbi { const Abi* Items; std::uint32_t Count; };
    constexpr auto Mapping = MapRecord<Model, Abi>(
        MapField<&Abi::Text>(Path<&Model::Text>{}),
        MapOptional<&Abi::X, &Abi::Mask>(Path<&Model::X>{}, 1),
        MapNullable<&Abi::Visible>(Path<&Model::Visible>{})
    );
    constexpr auto ListMapping = MapRecord<std::vector<Model>, ListAbi>(
        MapArray<&ListAbi::Items, &ListAbi::Count>(Identity{}, Mapping, 2048)
    );
    void Check(bool condition) { if (!condition) { throw std::runtime_error("Conversion generique incorrecte."); } }
    template<class Action> void Reject(Action action) {
        bool rejected = false;
        try { action(); } catch (const std::invalid_argument&) { rejected = true; }
        Check(rejected);
    }
}

void TestMappings() {
    const std::wstring unicode = L"\u00e9\U0001f642";
    Check(ToUtf8(unicode) == "\xc3\xa9\xf0\x9f\x99\x82");
    Check(FromUtf8(ToUtf8(unicode)) == unicode);
    Reject([] { FromUtf8("\xc0\xaf"); });
    Reject([] { FromUtf8("\xed\xa0\x80"); });
    Reject([] { FromUtf8("\xf4\x90\x80\x80"); });
    Reject([] { FromUtf8("\xf0\x9f"); });
    Reject([] { ToUtf8(std::wstring(1, static_cast<wchar_t>(0xd800))); });
    Reject([] { ToUtf8(std::wstring(1, L'\0')); });

    Model input{unicode, 0, false};
    Snapshot snapshot(Mapping);
    const Abi* output = snapshot.Set(input);
    input.Text = L"Autre";
    Check(Mapping.Read(output).Text == unicode && Mapping.Read(output).X == 0 && Mapping.Read(output).Visible == false);
    Model copy = Mapping.Read(output);
    copy.Text.clear();
    Check(Mapping.Read(output).Text == unicode);
    Reject([&] { snapshot.Set(Model{std::wstring(BufferStorage::MaxStringBytes + 1, L'x')}); });
    Check(Mapping.Read(output).Text == unicode);
    output = snapshot.Set(Model{});
    Check(!Mapping.Read(output).X && !Mapping.Read(output).Visible && output->Mask == 0 && output->Visible == -1);
    Abi invalid{nullptr, 0, 0, 2};
    Reject([&] { Mapping.Read(&invalid); });
    Reject([] { Mapping.Read(nullptr); });
    char borrowed[] = "abc";
    Abi borrowedAbi{borrowed, 0, 0, -1};
    Model owned = Mapping.Read(&borrowedAbi);
    borrowed[0] = 'z';
    Check(owned.Text == L"abc");

    std::vector<Model> many(2048, Model{unicode, 12, true});
    Snapshot listSnapshot(ListMapping);
    const ListAbi* list = listSnapshot.Set(many);
    many.clear();
    std::vector<Model> copied = ListMapping.Read(list);
    Check(copied.size() == 2048 && copied.front().Text == unicode && copied.back().Text == unicode);
    copied.back().Text = L"Modifie";
    Check(ListMapping.Read(list).back().Text == unicode);
    Reject([&] { listSnapshot.Set(std::vector<Model>(2049)); });
    Check(ListMapping.Read(list).size() == 2048);
    ListAbi invalidList{nullptr, 1};
    Reject([&] { ListMapping.Read(&invalidList); });
    invalidList.Count = 2049;
    Reject([&] { ListMapping.Read(&invalidList); });
    Check(ListMapping.Read(listSnapshot.Set({})).empty());
    std::cout << "PASS: copies, Unicode, options, tableaux bornes et conservation sur echec.\n";
}
