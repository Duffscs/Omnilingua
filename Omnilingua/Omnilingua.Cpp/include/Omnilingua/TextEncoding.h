#pragma once
#include <cstdint>
#include <stdexcept>
#include <string>
#include <string_view>
#include <type_traits>

namespace Omnilingua::Cpp {
    inline std::wstring FromUtf16(std::u16string_view text) {
        std::wstring result;
        for (std::size_t i = 0; i < text.size(); ++i) {
            std::uint32_t point = text[i];
            if (point >= 0xd800 && point <= 0xdbff) {
                if (++i == text.size() || text[i] < 0xdc00 || text[i] > 0xdfff) { throw std::invalid_argument("Paire UTF-16 invalide."); }
                if constexpr (sizeof(wchar_t) == 2) {
                    result.push_back(static_cast<wchar_t>(point));
                    point = text[i];
                } else { point = 0x10000 + ((point - 0xd800) << 10) + text[i] - 0xdc00; }
            } else if (point >= 0xdc00 && point <= 0xdfff) { throw std::invalid_argument("Surrogate UTF-16 isole."); }
            if (!point) { throw std::invalid_argument("Caractere nul interdit."); }
            result.push_back(static_cast<wchar_t>(point));
        }
        return result;
    }

    inline std::u16string ToUtf16(std::wstring_view text) {
        std::u16string result;
        if constexpr (sizeof(wchar_t) == 2) {
            result.assign(text.begin(), text.end());
            FromUtf16(result);
            return result;
        }
        for (wchar_t value : text) {
            std::uint32_t point = static_cast<std::make_unsigned_t<wchar_t>>(value);
            if constexpr (sizeof(wchar_t) == 2) { result.push_back(static_cast<char16_t>(point)); }
            else {
                if (!point || point > 0x10ffff || (point >= 0xd800 && point <= 0xdfff)) { throw std::invalid_argument("Point Unicode invalide."); }
                if (point >= 0x10000) {
                    point -= 0x10000;
                    result.push_back(static_cast<char16_t>(0xd800 + (point >> 10)));
                    point = 0xdc00 + (point & 0x3ff);
                }
                result.push_back(static_cast<char16_t>(point));
            }
        }
        return result;
    }
    // Conversion Unicode stricte, indépendante de Win32 et de la taille de wchar_t.
    inline std::wstring FromUtf8(std::string_view text) {
        std::wstring result;
        for (std::size_t i = 0; i < text.size();) {
            std::uint32_t point = static_cast<unsigned char>(text[i++]);
            unsigned count = 0;
            std::uint32_t minimum = 0;
            if (point >= 0xc2 && point <= 0xdf) { point &= 0x1f; count = 1; minimum = 0x80; }
            else if (point >= 0xe0 && point <= 0xef) { point &= 0x0f; count = 2; minimum = 0x800; }
            else if (point >= 0xf0 && point <= 0xf4) { point &= 7; count = 3; minimum = 0x10000; }
            else if (point >= 0x80 || point == 0) { throw std::invalid_argument("Chaine UTF-8 invalide."); }
            if (text.size() - i < count) { throw std::invalid_argument("Chaine UTF-8 tronquee."); }
            for (unsigned j = 0; j < count; ++j) {
                unsigned char next = static_cast<unsigned char>(text[i++]);
                if ((next & 0xc0) != 0x80) { throw std::invalid_argument("Continuation UTF-8 invalide."); }
                point = (point << 6) | (next & 0x3f);
            }
            if (point < minimum || point > 0x10ffff || (point >= 0xd800 && point <= 0xdfff)) {
                throw std::invalid_argument("Point Unicode invalide.");
            }
            if constexpr (sizeof(wchar_t) == 2) {
                if (point >= 0x10000) {
                    point -= 0x10000;
                    result.push_back(static_cast<wchar_t>(0xd800 + (point >> 10)));
                    result.push_back(static_cast<wchar_t>(0xdc00 + (point & 0x3ff)));
                    continue;
                }
            }
            result.push_back(static_cast<wchar_t>(point));
        }
        return result;
    }

    inline std::string ToUtf8(std::wstring_view text) {
        std::string result;
        for (std::size_t i = 0; i < text.size(); ++i) {
            std::uint32_t point = static_cast<std::make_unsigned_t<wchar_t>>(text[i]);
            if constexpr (sizeof(wchar_t) == 2) {
                if (point >= 0xd800 && point <= 0xdbff) {
                    if (++i == text.size()) { throw std::invalid_argument("Paire UTF-16 tronquee."); }
                    std::uint32_t low = static_cast<std::uint16_t>(text[i]);
                    if (low < 0xdc00 || low > 0xdfff) { throw std::invalid_argument("Paire UTF-16 invalide."); }
                    point = 0x10000 + ((point - 0xd800) << 10) + low - 0xdc00;
                } else if (point >= 0xdc00 && point <= 0xdfff) { throw std::invalid_argument("Surrogate UTF-16 isole."); }
            }
            if (!point || point > 0x10ffff || (point >= 0xd800 && point <= 0xdfff)) { throw std::invalid_argument("Point Unicode invalide."); }
            if (point < 0x80) { result.push_back(static_cast<char>(point)); }
            else {
                if (point < 0x800) { result.push_back(static_cast<char>(0xc0 | (point >> 6))); }
                else {
                    if (point < 0x10000) { result.push_back(static_cast<char>(0xe0 | (point >> 12))); }
                    else {
                        result.push_back(static_cast<char>(0xf0 | (point >> 18)));
                        result.push_back(static_cast<char>(0x80 | ((point >> 12) & 0x3f)));
                    }
                    result.push_back(static_cast<char>(0x80 | ((point >> 6) & 0x3f)));
                }
                result.push_back(static_cast<char>(0x80 | (point & 0x3f)));
            }
        }
        return result;
    }
}
