#pragma once
#include <Omnilingua/Export.h>
#include <cstdint>

namespace CodegenTests {
    class OMNILINGUA_PLUGIN_CONTRACT("ITest", 2) ITest : public virtual Omnilingua::Cpp::IService {
    public:
        virtual ~ITest() = default;
        virtual int Add(int left, int right) = 0;
        virtual bool IsEnabled() const noexcept = 0;
        virtual void SetEnabled(bool value) = 0;
        virtual unsigned int Size(unsigned int value) = 0;
        virtual unsigned long long Id(unsigned long long value) = 0;
        virtual std::uint64_t EchoId(std::uint64_t value) = 0;
    };
}
