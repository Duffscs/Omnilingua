#include <Omnilingua/NativeApi.h>
#include <Omnilingua/CallingConvention.h>
#include <stdexcept>
#include <iostream>
#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

class Library {
    void* _handle;
public:
    explicit Library(const char* file) {
#ifdef _WIN32
        _handle = LoadLibraryA(file);
#else
        _handle = dlopen(file, RTLD_NOW | RTLD_LOCAL);
#endif
        if (!_handle) { throw std::runtime_error("Chargement du plugin impossible."); }
    }
    ~Library() {
#ifdef _WIN32
        FreeLibrary(static_cast<HMODULE>(_handle));
#else
        dlclose(_handle);
#endif
    }
    template<class T> T Get(const char* name) {
#ifdef _WIN32
        auto address = GetProcAddress(static_cast<HMODULE>(_handle), name);
#else
        auto address = dlsym(_handle, name);
#endif
        if (!address) { throw std::runtime_error("Point d'entrée manquant."); }
        return reinterpret_cast<T>(address);
    }
};

int main(int count, char** args) {
    if (count != 3) { return 1; }
    Library logger(args[1]), hello(args[2]);
    using GetApi = const OmnilinguaPluginApi*(OMNILINGUA_CALL*)();
    const OmnilinguaPluginApi& provider = *logger.Get<GetApi>("OmnilinguaPlugin_GetApi")();
    const OmnilinguaPluginApi& consumer = *hello.Get<GetApi>("OmnilinguaPlugin_GetApi")();
    OmnilinguaPluginOptions options{};
    if (provider.Create(nullptr) != nullptr || !*provider.GetError(nullptr)) { return 2; }
    void* loggerHandle = provider.Create(&options);
    void* helloHandle = consumer.Create(&options);
    if (!loggerHandle || !helloHandle) { return 3; }
    if (consumer.Init(helloHandle, "{}") != 0 || !*consumer.GetError(helloHandle)) { return 4; }
    const PluginMetadata* required = consumer.GetMetadata(helloHandle);
    if (!required || required->DependencyCount != 1) { return 5; }
    if (!provider.Init(loggerHandle, "{}") || !provider.Start(loggerHandle)) { return 6; }
    void* secondLogger = provider.Create(&options);
    if (!secondLogger || !provider.Init(secondLogger, "{}")) { return 7; }
    const PluginContract& contract = required->Dependencies[0];
    for (std::uint32_t index = 0; index < contract.MethodCount; ++index) {
        void* address = provider.GetExport(loggerHandle, contract.Name, contract.Methods[index]);
        if (!address || !consumer.Inject(helloHandle, contract.Name, contract.Methods[index], address)) { return 8; }
    }
    if (!consumer.Init(helloHandle, "{}") || !consumer.Start(helloHandle)) { return 9; }
    if (!consumer.Stop(helloHandle) || !provider.Stop(loggerHandle)) { return 10; }
    if (!consumer.Destroy(helloHandle) || !provider.Destroy(loggerHandle) || !provider.Destroy(secondLogger)) { return 11; }
    std::cout << "PASS: API commune, erreurs, instances independantes et service injecte.\n";
}
