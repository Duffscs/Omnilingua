#include <jni.h>
#include <algorithm>
#include <Omnilingua/NativeApi.h>
#include <Omnilingua/CallingConvention.h>
#include <Omnilingua/TextEncoding.h>
#include <filesystem>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>
#include <thread>
#include <future>
#include <queue>
#include <condition_variable>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#define BRIDGE static
#else
#include <dlfcn.h>
#define BRIDGE static
#endif

namespace {
    void JNICALL InvokeImported(JNIEnv*, jclass, jlong, jstring, jstring, jobjectArray);
    thread_local bool OnWorker = false;
    JavaVM* Vm = nullptr;
    std::mutex VmGate;
    std::string SdkPath;
    thread_local std::string Error;
    // JNI reste sur un thread C++ dédié : la JVM ne modifie jamais les gardes
    // de pile d’un thread CLR ou d’un thread du plugin appelant.
    struct Dispatcher {
        std::mutex Gate;
        std::condition_variable Changed;
        std::queue<std::function<void()>> Tasks;
        Dispatcher() {
            std::thread([this] {
                OnWorker = true;
                for (;;) {
                    std::function<void()> task;
                    { std::unique_lock<std::mutex> lock(Gate); Changed.wait(lock, [&] { return !Tasks.empty(); }); task = std::move(Tasks.front()); Tasks.pop(); }
                    task();
                }
            }).detach();
        }
    };
    Dispatcher& Worker() { static Dispatcher* worker = new Dispatcher(); return *worker; }
    template<class Function> auto Dispatch(Function function) {
        if (OnWorker) { return function(); }
        using Result = decltype(function());
        Dispatcher* worker = &Worker();
        auto task = std::make_shared<std::packaged_task<std::pair<Result, std::string>()>>([function] {
            Error.clear();
            Result result = function();
            return std::make_pair(result, Error);
        });
        auto future = task->get_future();
        { std::lock_guard<std::mutex> lock(worker->Gate); worker->Tasks.push([task] { (*task)(); }); }
        worker->Changed.notify_one();
        auto [result, error] = future.get();
        Error = std::move(error);
        return result;
    }
    const AbiTypeDescription Void{AbiKind::Void, "void", 0, nullptr, 0, nullptr};
    const AbiTypeDescription Utf8{AbiKind::Utf8, "utf8", sizeof(void*), nullptr, 0, nullptr};

    std::string Text(JNIEnv* env, jstring text) {
        if (!text) { return {}; }
        const jchar* chars = env->GetStringChars(text, nullptr);
        if (!chars) { throw std::runtime_error("Lecture d’une chaine Java impossible."); }
        std::u16string copy(chars, chars + env->GetStringLength(text));
        env->ReleaseStringChars(text, chars);
        return Omnilingua::Cpp::ToUtf8(Omnilingua::Cpp::FromUtf16(copy));
    }
    void Check(JNIEnv* env) {
        if (!env->ExceptionCheck()) { return; }
        jthrowable exception = env->ExceptionOccurred();
        env->ExceptionClear();
        jclass type = env->GetObjectClass(exception);
        jmethodID method = env->GetMethodID(type, "toString", "()Ljava/lang/String;");
        jstring message = method ? static_cast<jstring>(env->CallObjectMethod(exception, method)) : nullptr;
        if (env->ExceptionCheck()) { env->ExceptionClear(); throw std::runtime_error("Exception Java (diagnostic indisponible)."); }
        throw std::runtime_error(message ? Text(env, message) : "Exception Java.");
    }
    jstring String(JNIEnv* env, const char* value) {
        if (!value) { return nullptr; }
        std::u16string text = Omnilingua::Cpp::ToUtf16(Omnilingua::Cpp::FromUtf8(value));
        std::vector<jchar> characters(text.begin(), text.end());
        jstring result = env->NewString(characters.data(), static_cast<jsize>(characters.size()));
        Check(env);
        return result;
    }
    void EnsureVm(const char* library, const char* sdk) {
        std::lock_guard<std::mutex> lock(VmGate);
        if (Vm) {
            if (SdkPath != sdk) { throw std::runtime_error("La JVM existe avec une autre bibliothèque Omnilingua.Java."); }
            return;
        }
#ifdef _WIN32
        HMODULE module = LoadLibraryExW(Omnilingua::Cpp::FromUtf8(library).c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
        auto create = module ? reinterpret_cast<jint(JNICALL*)(JavaVM**, void**, void*)>(GetProcAddress(module, "JNI_CreateJavaVM")) : nullptr;
#else
        void* module = dlopen(library, RTLD_NOW | RTLD_GLOBAL);
        auto create = module ? reinterpret_cast<jint(*)(JavaVM**, void**, void*)>(dlsym(module, "JNI_CreateJavaVM")) : nullptr;
#endif
        if (!create) { throw std::runtime_error("JNI_CreateJavaVM introuvable : vérifier le runtime Java et son architecture."); }
        std::string classPath = std::string("-Djava.class.path=") + sdk;
        char reducedSignals[] = "-Xrs";
        JavaVMOption options[2]{{classPath.data(), nullptr}, {reducedSignals, nullptr}};
        JavaVMInitArgs args{JNI_VERSION_1_8, 2, options, JNI_FALSE};
        JNIEnv* env = nullptr;
        jint result = create(&Vm, reinterpret_cast<void**>(&env), &args);
        if (result != JNI_OK) { Vm = nullptr; throw std::runtime_error("Création de JVM impossible : " + std::to_string(result)); }
        jclass runtime = env->FindClass("omnilingua/java/RuntimePlugin"); Check(env);
        JNINativeMethod imports[]{{const_cast<char*>("invokeImport"), const_cast<char*>("(JLjava/lang/String;Ljava/lang/String;[Ljava/lang/String;)V"), reinterpret_cast<void*>(&InvokeImported)}};
        if (env->RegisterNatives(runtime, imports, 1) != JNI_OK) { Check(env); throw std::runtime_error("Enregistrement JNI impossible."); }
        env->DeleteLocalRef(runtime);
        SdkPath = sdk;
        // Le thread créateur reste attaché : détacher le dernier thread non daemon
        // peut provoquer l’arrêt du processus par la JVM.
        // JVM et bibliothèque restent chargées jusqu’à la fin du processus.
    }
    struct Frame {
        JNIEnv* Env = nullptr;
        bool Attached = false;
        Frame() {
            jint status = Vm->GetEnv(reinterpret_cast<void**>(&Env), JNI_VERSION_1_8);
            if (status == JNI_EDETACHED) {
                if (Vm->AttachCurrentThreadAsDaemon(reinterpret_cast<void**>(&Env), nullptr) != JNI_OK) { throw std::runtime_error("Attachement du thread à Java impossible."); }
                Attached = true;
            } else if (status != JNI_OK) { throw std::runtime_error("Environnement JNI indisponible."); }
            if (Env->PushLocalFrame(128) != JNI_OK) {
                if (Attached) { Vm->DetachCurrentThread(); }
                throw std::runtime_error("Allocation des références JNI impossible.");
            }
        }
        ~Frame() { Env->PopLocalFrame(nullptr); if (Attached) { Vm->DetachCurrentThread(); } }
    };
    struct Contract {
        std::string Name;
        std::uint32_t Version;
        std::vector<std::string> Names;
        std::vector<int> Indices;
        std::vector<const char*> NamePointers;
        std::vector<std::vector<AbiParameterDescription>> Parameters;
        std::vector<AbiMethodDescription> Methods;
        bool Optional = false;
    };
    struct Plugin {
        jobject Instance = nullptr;
        jclass Type = nullptr;
        jmethodID Invoke = nullptr;
        std::vector<Contract> Contracts;
        std::vector<PluginContract> Exports;
        std::vector<Contract> ImportedContracts;
        std::vector<PluginContract> Dependencies;
        using ImportCallback = int(OMNILINGUA_CALL*)(void*, const char*, const char*, const char**, int);
        ImportCallback Callback = nullptr;
        PluginMetadata Metadata{};
    };
    void JNICALL InvokeImported(JNIEnv* env, jclass, jlong handle, jstring contract, jstring method, jobjectArray arguments) {
        try {
            if (handle == 0) { throw std::runtime_error("Plugin ferme."); }
            auto* plugin = reinterpret_cast<Plugin*>(static_cast<std::intptr_t>(handle));
            if (!plugin->Callback) { throw std::runtime_error("Service non injecte."); }
            jsize count = env->GetArrayLength(arguments);
            if (count > 16) { throw std::runtime_error("Trop de parametres Java."); }
            std::vector<std::string> values(count);
            std::vector<const char*> pointers(count);
            for (jsize index = 0; index < count; ++index) {
                auto item = static_cast<jstring>(env->GetObjectArrayElement(arguments, index)); Check(env);
                if (item) { values[index] = Text(env, item); pointers[index] = values[index].c_str(); env->DeleteLocalRef(item); }
            }
            std::string name = Text(env, contract), operation = Text(env, method);
            if (!plugin->Callback(plugin, name.c_str(), operation.c_str(), pointers.data(), count)) { throw std::runtime_error("Echec du fournisseur de " + name + "." + operation); }
        } catch (const std::exception& exception) {
            if (env->ExceptionCheck()) { env->ExceptionClear(); }
            jclass error = env->FindClass("java/lang/IllegalStateException");
            if (error) { env->ThrowNew(error, exception.what()); env->DeleteLocalRef(error); }
        }
    }

}


void* OpenInternal(const char* library, const char* sdk, const char* jar, const char* entry) noexcept {
    try {
        Error.clear();
        EnsureVm(library, sdk);
        Frame frame;
        JNIEnv* env = frame.Env;
        jclass type = env->FindClass("omnilingua/java/RuntimePlugin"); Check(env);
        jmethodID load = env->GetStaticMethodID(type, "load", "(Ljava/lang/String;Ljava/lang/String;)Lomnilingua/java/RuntimePlugin;"); Check(env);
        jobject instance = env->CallStaticObjectMethod(type, load, String(env, jar), String(env, entry)); Check(env);
        auto plugin = std::make_unique<Plugin>();
        jmethodID describe = env->GetMethodID(type, "describe", "()[Ljava/lang/String;"); Check(env);
        auto rows = static_cast<jobjectArray>(env->CallObjectMethod(instance, describe)); Check(env);
        for (jsize i = 0; i < env->GetArrayLength(rows); ++i) {
            auto row = static_cast<jstring>(env->GetObjectArrayElement(rows, i)); Check(env);
            std::istringstream stream(Text(env, row));
            std::string name, version, method, count;
            std::getline(stream, name, '\t'); std::getline(stream, version, '\t'); std::getline(stream, method, '\t'); std::getline(stream, count);
            auto found = std::find_if(plugin->Contracts.begin(), plugin->Contracts.end(), [&](const Contract& contract) { return contract.Name == name; });
            if (found == plugin->Contracts.end()) { plugin->Contracts.push_back({name, static_cast<std::uint32_t>(std::stoul(version))}); found = std::prev(plugin->Contracts.end()); }
            if (found->Version != std::stoul(version)) { throw std::runtime_error("Versions Java incompatibles pour " + name); }
            found->Names.push_back(method);
            found->Indices.push_back(i);
            found->Parameters.emplace_back(std::stoul(count), AbiParameterDescription{"argument", &Utf8, AbiPassing::Value});
            env->DeleteLocalRef(row);
        }
        for (Contract& contract : plugin->Contracts) {
            for (std::size_t i = 0; i < contract.Names.size(); ++i) {
                contract.NamePointers.push_back(contract.Names[i].c_str());
                contract.Methods.push_back({contract.Names[i].c_str(), &Void, AbiPassing::Value,
                    contract.Parameters[i].data(), static_cast<std::uint32_t>(contract.Parameters[i].size())});
            }
            plugin->Exports.push_back({contract.Name.c_str(), contract.NamePointers.data(), static_cast<std::uint32_t>(contract.Names.size()),
                contract.Version, contract.Methods.data(), static_cast<std::uint32_t>(contract.Methods.size())});
        }
        jmethodID describeImports = env->GetMethodID(type, "describeImports", "()[Ljava/lang/String;"); Check(env);
        auto importRows = static_cast<jobjectArray>(env->CallObjectMethod(instance, describeImports)); Check(env);
        for (jsize index = 0; index < env->GetArrayLength(importRows); ++index) {
            auto row = static_cast<jstring>(env->GetObjectArrayElement(importRows, index)); Check(env);
            std::istringstream stream(Text(env, row));
            std::string name, version, method, count, optional;
            std::getline(stream, name, '\t'); std::getline(stream, version, '\t'); std::getline(stream, method, '\t'); std::getline(stream, count, '\t'); std::getline(stream, optional);
            auto found = std::find_if(plugin->ImportedContracts.begin(), plugin->ImportedContracts.end(), [&](const Contract& entry) { return entry.Name == name; });
            if (found == plugin->ImportedContracts.end()) { plugin->ImportedContracts.push_back({name, static_cast<std::uint32_t>(std::stoul(version))}); found = std::prev(plugin->ImportedContracts.end()); found->Optional = optional == "1"; }
            found->Names.push_back(method);
            found->Parameters.emplace_back(std::stoul(count), AbiParameterDescription{"argument", &Utf8, AbiPassing::Value});
            env->DeleteLocalRef(row);
        }
        for (Contract& contract : plugin->ImportedContracts) {
            for (std::size_t index = 0; index < contract.Names.size(); ++index) {
                contract.NamePointers.push_back(contract.Names[index].c_str());
                contract.Methods.push_back({contract.Names[index].c_str(), &Void, AbiPassing::Value, contract.Parameters[index].data(), static_cast<std::uint32_t>(contract.Parameters[index].size())});
            }
            plugin->Dependencies.push_back({contract.Name.c_str(), contract.NamePointers.data(), static_cast<std::uint32_t>(contract.Names.size()), contract.Version, contract.Methods.data(), static_cast<std::uint32_t>(contract.Methods.size()), contract.Optional ? 1u : 0u});
        }
        plugin->Invoke = env->GetMethodID(type, "invoke", "(I[Ljava/lang/String;)V"); Check(env);
        plugin->Metadata = {3, plugin->Dependencies.data(), static_cast<std::uint32_t>(plugin->Dependencies.size()), plugin->Exports.data(), static_cast<std::uint32_t>(plugin->Exports.size())};
        plugin->Instance = env->NewGlobalRef(instance);
        plugin->Type = static_cast<jclass>(env->NewGlobalRef(type)); Check(env);
        return plugin.release();
    } catch (const std::exception& error) { Error = error.what(); return nullptr; }
}

BRIDGE const PluginMetadata* Java_Metadata(void* handle) noexcept { return &static_cast<Plugin*>(handle)->Metadata; }

BRIDGE int Java_ExportIndex(void* handle, const char* contract, const char* method) noexcept {
    for (const Contract& entry : static_cast<Plugin*>(handle)->Contracts) {
        if (entry.Name != contract) { continue; }
        for (std::size_t i = 0; i < entry.Names.size(); ++i) { if (entry.Names[i] == method) { return entry.Indices[i]; } }
    }
    return -1;
}

int LifecycleInternal(void* handle, const char* operation, const char* configuration) noexcept {
    try {
        Frame frame; JNIEnv* env = frame.Env; auto& plugin = *static_cast<Plugin*>(handle);
        bool init = std::string(operation) == "init";
        jmethodID method = env->GetMethodID(plugin.Type, operation, init ? "(Ljava/lang/String;)V" : "()V"); Check(env);
        if (init) { env->CallVoidMethod(plugin.Instance, method, String(env, configuration)); }
        else { env->CallVoidMethod(plugin.Instance, method); }
        Check(env); return 1;
    } catch (const std::exception& error) { Error = error.what(); return 0; }
}

int InvokeInternal(void* handle, int index, const char** arguments, int count) noexcept {
    try {
        Frame frame; JNIEnv* env = frame.Env; auto& plugin = *static_cast<Plugin*>(handle);
        jclass strings = env->FindClass("java/lang/String"); Check(env);
        jobjectArray array = env->NewObjectArray(count, strings, nullptr); Check(env);
        for (int i = 0; i < count; ++i) { env->SetObjectArrayElement(array, i, String(env, arguments[i])); Check(env); }
        env->CallVoidMethod(plugin.Instance, plugin.Invoke, index, array); Check(env);
        return 1;
    } catch (const std::exception& error) { Error = error.what(); return 0; }
}

void CloseInternal(void* handle) noexcept {
    std::unique_ptr<Plugin> plugin(static_cast<Plugin*>(handle));
    if (!plugin) { return; }
    LifecycleInternal(handle, "close", nullptr);
    try { Frame frame; frame.Env->DeleteGlobalRef(plugin->Instance); frame.Env->DeleteGlobalRef(plugin->Type); }
    catch (const std::exception& error) { Error = error.what(); }
}

BRIDGE void* Java_Open(const char* library, const char* sdk, const char* jar, const char* entry) noexcept {
    try { return Dispatch([=] { return OpenInternal(library, sdk, jar, entry); }); }
    catch (const std::exception& error) { Error = error.what(); return nullptr; }
}
BRIDGE int Java_Lifecycle(void* handle, const char* operation, const char* configuration) noexcept {
    try { return Dispatch([=] { return LifecycleInternal(handle, operation, configuration); }); }
    catch (const std::exception& error) { Error = error.what(); return 0; }
}
BRIDGE int Java_Invoke(void* handle, int index, const char** arguments, int count) noexcept {
    try { return Dispatch([=] { return InvokeInternal(handle, index, arguments, count); }); }
    catch (const std::exception& error) { Error = error.what(); return 0; }
}
BRIDGE void Java_Close(void* handle) noexcept {
    try { Dispatch([=] { CloseInternal(handle); return 1; }); }
    catch (const std::exception& error) { Error = error.what(); }
}

BRIDGE int Java_BindImports(void* handle, const char* contract, void* callback) noexcept {
    try {
        return Dispatch([=] {
            try {
                Frame frame; auto* plugin = static_cast<Plugin*>(handle);
                plugin->Callback = reinterpret_cast<Plugin::ImportCallback>(callback);
                jmethodID bind = frame.Env->GetMethodID(plugin->Type, "bind", "(Ljava/lang/String;J)V"); Check(frame.Env);
                frame.Env->CallVoidMethod(plugin->Instance, bind, String(frame.Env, contract), static_cast<jlong>(reinterpret_cast<std::intptr_t>(plugin))); Check(frame.Env);
                return 1;
            } catch (const std::exception& exception) { Error = exception.what(); return 0; }
        });
    } catch (const std::exception& exception) { Error = exception.what(); return 0; }
}

namespace {
    void* OMNILINGUA_CALL CreatePlugin(const OmnilinguaPluginOptions* options) noexcept {
        if (!options) { Error = "Options absentes."; return nullptr; }
        return Java_Open(options->RuntimeLibrary, options->Sdk, options->Assembly, options->EntryPoint);
    }
    std::int32_t OMNILINGUA_CALL InitPlugin(void* handle, const char* config) noexcept { return Java_Lifecycle(handle, "init", config); }
    std::int32_t OMNILINGUA_CALL StartPlugin(void* handle) noexcept { return Java_Lifecycle(handle, "start", nullptr); }
    std::int32_t OMNILINGUA_CALL StopPlugin(void* handle) noexcept { return Java_Lifecycle(handle, "stop", nullptr); }
    std::int32_t OMNILINGUA_CALL DestroyPlugin(void* handle) noexcept { Error.clear(); Java_Close(handle); return Error.empty() ? 1 : 0; }
    const char* OMNILINGUA_CALL PluginError(void*) noexcept { return Error.c_str(); }
    std::int32_t OMNILINGUA_CALL InvokePlugin(void* handle, const char* contract, const char* method, const char** args, std::int32_t count) noexcept {
        int index = Java_ExportIndex(handle, contract, method);
        if (index < 0) { Error = "Export absent."; return 0; }
        return Java_Invoke(handle, index, args, count);
    }
}
PLUGIN_EXPORT const OmnilinguaPluginApi* OMNILINGUA_CALL OmnilinguaPlugin_GetApi() noexcept {
    static const OmnilinguaPluginApi api{OmnilinguaPluginDynamicCalls | OmnilinguaPluginCallbackImports | OmnilinguaPluginProcessLifetime, 0,
        CreatePlugin, Java_Metadata, InitPlugin, StartPlugin, StopPlugin, nullptr, nullptr, DestroyPlugin, PluginError, InvokePlugin, Java_BindImports};
    return &api;
}
