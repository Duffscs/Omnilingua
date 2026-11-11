// Ne pas lier python3xx.lib : le runtime utilisateur est résolu dynamiquement.
#define PY_SSIZE_T_CLEAN
#define MS_NO_COREDLL
#include <Python.h>
#include <Omnilingua/NativeApi.h>
#include <Omnilingua/TextEncoding.h>
#include <algorithm>
#include <condition_variable>
#include <future>
#include <memory>
#include <mutex>
#include <queue>
#include <sstream>
#include <thread>
#include <vector>
#include <utility>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#define BRIDGE static
#else
#include <dlfcn.h>
#define BRIDGE static
#endif

namespace {
    thread_local std::string Error;
    thread_local bool OnWorker = false;
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
    Dispatcher& Worker() { static auto* worker = new Dispatcher(); return *worker; }
    template<class Function> auto Dispatch(Function function) {
        using Result = decltype(function());
        if (OnWorker) { return function(); }
        auto task = std::make_shared<std::packaged_task<std::pair<Result, std::string>()>>([function] {
            Error.clear(); Result result = function(); return std::make_pair(result, Error);
        });
        auto future = task->get_future();
        { std::lock_guard<std::mutex> lock(Worker().Gate); Worker().Tasks.push([task] { (*task)(); }); }
        Worker().Changed.notify_one();
        auto [result, error] = future.get(); Error = std::move(error); return result;
    }

    // Les signatures proviennent des headers du CPython ciblé à la compilation.
    struct PythonApi {
#define PY_API(name) decltype(&::name) name = nullptr
        PY_API(Py_GetVersion); PY_API(Py_IsInitialized);
        PY_API(PyConfig_InitIsolatedConfig); PY_API(PyConfig_SetString);
        PY_API(Py_InitializeFromConfig); PY_API(PyConfig_Clear); PY_API(PyStatus_Exception);
        PY_API(PyEval_SaveThread); PY_API(PyGILState_Ensure); PY_API(PyGILState_Release);
        PY_API(PyImport_ImportModule); PY_API(PyObject_GetAttrString); PY_API(PyObject_CallObject);
        PY_API(PyTuple_New); PY_API(PyTuple_SetItem); PY_API(PyUnicode_FromString);
        PY_API(PyUnicode_AsUTF8AndSize); PY_API(PyLong_FromLong);
        PY_API(PyList_Size); PY_API(PyList_GetItem); PY_API(PyList_Insert); PY_API(PySys_GetObject);
        PY_API(Py_DecRef); PY_API(Py_IncRef); PY_API(PyErr_Fetch); PY_API(PyErr_NormalizeException);
        PY_API(PyErr_Clear); PY_API(PyObject_Str);
#undef PY_API
        void Load(const char* library) {
#ifdef _WIN32
            HMODULE module = LoadLibraryExW(Omnilingua::Cpp::FromUtf8(library).c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
            if (!module) { throw std::runtime_error("Bibliothèque CPython impossible à charger."); }
            auto resolve = [&](const char* symbol) { return GetProcAddress(module, symbol); };
#else
            void* module = dlopen(library, RTLD_NOW | RTLD_GLOBAL);
            if (!module) { throw std::runtime_error("Bibliothèque CPython impossible à charger."); }
            auto resolve = [&](const char* symbol) { return dlsym(module, symbol); };
#endif
#define LOAD(name) name = reinterpret_cast<decltype(name)>(resolve(#name)); if (!name) { throw std::runtime_error("Symbole CPython absent : " #name); }
            LOAD(Py_GetVersion); LOAD(Py_IsInitialized);
            LOAD(PyConfig_InitIsolatedConfig); LOAD(PyConfig_SetString);
            LOAD(Py_InitializeFromConfig); LOAD(PyConfig_Clear); LOAD(PyStatus_Exception);
            LOAD(PyEval_SaveThread); LOAD(PyGILState_Ensure); LOAD(PyGILState_Release);
            LOAD(PyImport_ImportModule); LOAD(PyObject_GetAttrString); LOAD(PyObject_CallObject);
            LOAD(PyTuple_New); LOAD(PyTuple_SetItem); LOAD(PyUnicode_FromString);
            LOAD(PyUnicode_AsUTF8AndSize); LOAD(PyLong_FromLong);
            LOAD(PyList_Size); LOAD(PyList_GetItem); LOAD(PyList_Insert); LOAD(PySys_GetObject);
            LOAD(Py_DecRef); LOAD(Py_IncRef); LOAD(PyErr_Fetch); LOAD(PyErr_NormalizeException);
            LOAD(PyErr_Clear); LOAD(PyObject_Str);
#undef LOAD
            int major = 0, minor = 0;
            std::istringstream version(Py_GetVersion()); char dot;
            version >> major >> dot >> minor;
            if (major != PY_MAJOR_VERSION || minor != PY_MINOR_VERSION) {
                throw std::runtime_error("Version CPython incompatible avec le pont : " + std::to_string(PY_MAJOR_VERSION) + "." + std::to_string(PY_MINOR_VERSION) + " requise.");
            }
        }
    } Api;
    bool Initialized = false;
    std::string LibraryPath, SdkPath;

    struct Object {
        PyObject* Value;
        explicit Object(PyObject* value = nullptr) : Value(value) {}
        ~Object() { if (Value) { Api.Py_DecRef(Value); } }
        Object(const Object&) = delete;
        Object& operator=(const Object&) = delete;
        PyObject* Release() { return std::exchange(Value, nullptr); }
    };
    std::string ExceptionText() {
        PyObject *type = nullptr, *value = nullptr, *trace = nullptr;
        Api.PyErr_Fetch(&type, &value, &trace);
        if (!type) { return "Erreur CPython sans exception disponible."; }
        Api.PyErr_NormalizeException(&type, &value, &trace);
        Object ownType(type), ownValue(value), ownTrace(trace);
        Object module(Api.PyImport_ImportModule("traceback"));
        Object formatter(module.Value ? Api.PyObject_GetAttrString(module.Value, "format_exception") : nullptr);
        if (formatter.Value && value) {
            Object args(Api.PyTuple_New(3));
            if (args.Value) {
                Api.Py_IncRef(type); Api.PyTuple_SetItem(args.Value, 0, type);
                Api.Py_IncRef(value); Api.PyTuple_SetItem(args.Value, 1, value);
                // Une trace absente est représentée par None, obtenu depuis builtins.
                Object builtins(Api.PyImport_ImportModule("builtins"));
                PyObject* traceValue = trace;
                Object none(builtins.Value ? Api.PyObject_GetAttrString(builtins.Value, "None") : nullptr);
                if (!traceValue) { traceValue = none.Value; }
                if (traceValue) {
                    Api.Py_IncRef(traceValue); Api.PyTuple_SetItem(args.Value, 2, traceValue);
                    Object lines(Api.PyObject_CallObject(formatter.Value, args.Value));
                    if (lines.Value) {
                        std::string text;
                        Py_ssize_t count = Api.PyList_Size(lines.Value);
                        for (Py_ssize_t i = 0; i < count; ++i) {
                            Py_ssize_t size = 0;
                            const char* line = Api.PyUnicode_AsUTF8AndSize(Api.PyList_GetItem(lines.Value, i), &size);
                            if (line) { text.append(line, size); }
                        }
                        Api.PyErr_Clear();
                        if (!text.empty()) { return text; }
                    }
                }
            }
        }
        Api.PyErr_Clear();
        Object message(value ? Api.PyObject_Str(value) : nullptr);
        const char* text = message.Value ? Api.PyUnicode_AsUTF8AndSize(message.Value, nullptr) : nullptr;
        std::string result = text ? text : "Exception Python (diagnostic indisponible).";
        Api.PyErr_Clear(); return result;
    }
    PyObject* Checked(PyObject* value) {
        if (!value) { throw std::runtime_error(ExceptionText()); }
        return value;
    }
    std::string Text(PyObject* value) {
        Py_ssize_t size = 0;
        const char* text = Api.PyUnicode_AsUTF8AndSize(value, &size);
        if (!text) { throw std::runtime_error(ExceptionText()); }
        return {text, static_cast<std::size_t>(size)};
    }
    void SetItem(PyObject* tuple, Py_ssize_t index, Object& item) {
        if (Api.PyTuple_SetItem(tuple, index, item.Release()) != 0) { throw std::runtime_error(ExceptionText()); }
    }
    Object Call(PyObject* object, const char* name, PyObject* args = nullptr) {
        Object method(Checked(Api.PyObject_GetAttrString(object, name)));
        return Object(Checked(Api.PyObject_CallObject(method.Value, args)));
    }
    struct Gil {
        PyGILState_STATE State = Api.PyGILState_Ensure();
        ~Gil() { Api.PyGILState_Release(State); }
    };

    void Initialize(const char* library, const char* home, const char* executable, const char* sdk) {
        if (Initialized) {
            if (LibraryPath != library || SdkPath != sdk) { throw std::runtime_error("Le processus utilise déjà une autre installation CPython ou un autre SDK Omnilingua.Python."); }
            return;
        }
        Api.Load(library);
        if (Api.Py_IsInitialized()) { throw std::runtime_error("Un autre composant a déjà initialisé CPython dans ce processus."); }
        PyConfig config;
        Api.PyConfig_InitIsolatedConfig(&config);
        config.install_signal_handlers = 0;
        config.parse_argv = 0;
        config.write_bytecode = 0;
        std::wstring pythonHome = Omnilingua::Cpp::FromUtf8(home), program = Omnilingua::Cpp::FromUtf8(executable);
        PyStatus status = Api.PyConfig_SetString(&config, &config.home, pythonHome.c_str());
        if (!Api.PyStatus_Exception(status)) { status = Api.PyConfig_SetString(&config, &config.program_name, program.c_str()); }
        if (!Api.PyStatus_Exception(status)) { status = Api.PyConfig_SetString(&config, &config.stdio_encoding, L"utf-8"); }
        if (!Api.PyStatus_Exception(status)) { status = Api.PyConfig_SetString(&config, &config.stdio_errors, L"strict"); }
        if (!Api.PyStatus_Exception(status)) { status = Api.Py_InitializeFromConfig(&config); }
        std::string error = Api.PyStatus_Exception(status) ? (status.err_msg ? status.err_msg : "Initialisation CPython impossible.") : "";
        Api.PyConfig_Clear(&config);
        if (!error.empty()) { throw std::runtime_error(error); }
        // Libère le GIL lorsque le worker attend : les threads internes Python peuvent travailler.
        Api.PyEval_SaveThread();
        Initialized = true; LibraryPath = library; SdkPath = sdk;
        Gil gil;
        Object path(Checked(Api.PyUnicode_FromString(sdk)));
        if (Api.PyList_Insert(Api.PySys_GetObject("path"), 0, path.Value) != 0) { throw std::runtime_error(ExceptionText()); }
    }

    const AbiTypeDescription Void{AbiKind::Void, "void", 0, nullptr, 0, nullptr};
    const AbiTypeDescription Utf8{AbiKind::Utf8, "utf8", sizeof(void*), nullptr, 0, nullptr};
    struct Contract {
        std::string Name;
        std::uint32_t Version;
        bool Optional = false;
        std::vector<std::string> Names;
        std::vector<int> Indices;
        std::vector<const char*> NamePointers;
        std::vector<std::vector<AbiParameterDescription>> Parameters;
        std::vector<AbiMethodDescription> Methods;
    };
    struct Plugin {
        Object Instance;
        std::vector<Contract> Contracts;
        std::vector<PluginContract> Exports;
        std::vector<Contract> ImportedContracts;
        std::vector<PluginContract> Dependencies;
        PluginMetadata Metadata{};
    };
    void* OpenInternal(const char* library, const char* home, const char* executable, const char* sdk, const char* file, const char* entry) {
        Initialize(library, home, executable, sdk);
        Gil gil;
        Object module(Checked(Api.PyImport_ImportModule("omnilingua_python")));
        Object runtime(Checked(Api.PyObject_GetAttrString(module.Value, "RuntimePlugin")));
        Object args(Checked(Api.PyTuple_New(2))), fileName(Checked(Api.PyUnicode_FromString(file))), entryName(Checked(Api.PyUnicode_FromString(entry)));
        SetItem(args.Value, 0, fileName); SetItem(args.Value, 1, entryName);
        Object instance = Call(runtime.Value, "load", args.Value);
        auto plugin = std::make_unique<Plugin>();
        plugin->Instance.Value = instance.Release();
        try {
            auto describe = [&](const char* operation, std::vector<Contract>& contracts, std::vector<PluginContract>& output, bool imports) {
                Object rows = Call(plugin->Instance.Value, operation);
                Py_ssize_t count = Api.PyList_Size(rows.Value);
                if (count < 0 || count > 4096) { throw std::runtime_error("Nombre de méthodes Python invalide."); }
                for (Py_ssize_t i = 0; i < count; ++i) {
                    std::istringstream stream(Text(Checked(Api.PyList_GetItem(rows.Value, i))));
                    std::string name, version, method, parameters, optional;
                    std::getline(stream, name, '\t'); std::getline(stream, version, '\t'); std::getline(stream, method, '\t');
                    std::getline(stream, parameters, '\t'); std::getline(stream, optional);
                    auto found = std::find_if(contracts.begin(), contracts.end(), [&](const Contract& contract) { return contract.Name == name; });
                    if (found == contracts.end()) {
                        contracts.push_back({name, static_cast<std::uint32_t>(std::stoul(version)), imports && optional == "1"});
                        found = std::prev(contracts.end());
                    }
                    if (found->Version != std::stoul(version) || std::stoul(parameters) > 16) { throw std::runtime_error("Description Python incompatible."); }
                    found->Names.push_back(method); found->Indices.push_back(static_cast<int>(i));
                    found->Parameters.emplace_back(std::stoul(parameters), AbiParameterDescription{"argument", &Utf8, AbiPassing::Value});
                }
                for (Contract& contract : contracts) {
                    for (std::size_t i = 0; i < contract.Names.size(); ++i) {
                        contract.NamePointers.push_back(contract.Names[i].c_str());
                        contract.Methods.push_back({contract.Names[i].c_str(), &Void, AbiPassing::Value, contract.Parameters[i].data(), static_cast<std::uint32_t>(contract.Parameters[i].size())});
                    }
                    output.push_back({contract.Name.c_str(), contract.NamePointers.data(), static_cast<std::uint32_t>(contract.Names.size()), contract.Version,
                        contract.Methods.data(), static_cast<std::uint32_t>(contract.Methods.size()), contract.Optional ? 1u : 0u});
                }
            };
            describe("describe", plugin->Contracts, plugin->Exports, false);
            describe("describe_imports", plugin->ImportedContracts, plugin->Dependencies, true);
            plugin->Metadata = {3, plugin->Dependencies.data(), static_cast<std::uint32_t>(plugin->Dependencies.size()),
                plugin->Exports.data(), static_cast<std::uint32_t>(plugin->Exports.size())};
            return plugin.release();
        } catch (...) {
            try { Call(plugin->Instance.Value, "close"); } catch (...) {}
            throw;
        }
    }
}

BRIDGE const PluginMetadata* Python_Metadata(void* handle) noexcept { return &static_cast<Plugin*>(handle)->Metadata; }
BRIDGE int Python_ExportIndex(void* handle, const char* contract, const char* method) noexcept {
    for (const Contract& value : static_cast<Plugin*>(handle)->Contracts) {
        if (value.Name != contract) { continue; }
        for (std::size_t i = 0; i < value.Names.size(); ++i) { if (value.Names[i] == method) { return value.Indices[i]; } }
    }
    return -1;
}
BRIDGE void* Python_Open(const char* library, const char* home, const char* executable, const char* sdk, const char* file, const char* entry) noexcept {
    try { return Dispatch([=] { return OpenInternal(library, home, executable, sdk, file, entry); }); }
    catch (const std::exception& error) { Error = error.what(); return nullptr; }
}
BRIDGE int Python_Lifecycle(void* handle, const char* operation, const char* configuration) noexcept {
    try {
        return Dispatch([=] {
            Gil gil;
            Object args;
            if (std::string(operation) == "init") {
                args.Value = Checked(Api.PyTuple_New(1));
                Object config(Checked(Api.PyUnicode_FromString(configuration ? configuration : "{}"))); SetItem(args.Value, 0, config);
            }
            Call(static_cast<Plugin*>(handle)->Instance.Value, operation, args.Value);
            return 1;
        });
    } catch (const std::exception& error) { Error = error.what(); return 0; }
}
BRIDGE int Python_Invoke(void* handle, int index, const char** arguments, int count) noexcept {
    try {
        return Dispatch([=] {
            Gil gil;
            Object strings(Checked(Api.PyTuple_New(count)));
            for (int i = 0; i < count; ++i) {
                if (!arguments[i]) { throw std::invalid_argument("Paramètre str Python nul."); }
                Object text(Checked(Api.PyUnicode_FromString(arguments[i]))); SetItem(strings.Value, i, text);
            }
            Object args(Checked(Api.PyTuple_New(2))), number(Checked(Api.PyLong_FromLong(index)));
            SetItem(args.Value, 0, number); SetItem(args.Value, 1, strings);
            Call(static_cast<Plugin*>(handle)->Instance.Value, "invoke", args.Value); return 1;
        });
    } catch (const std::exception& error) { Error = error.what(); return 0; }
}
BRIDGE void Python_Close(void* handle) noexcept {
    if (!handle) { return; }
    try {
        Dispatch([=] {
            Gil gil;
            std::unique_ptr<Plugin> plugin(static_cast<Plugin*>(handle));
            Call(plugin->Instance.Value, "close"); return 1;
        });
    } catch (const std::exception& error) { Error = error.what(); }
}

// Le SDK construit ses proxies ctypes ; aucun pointeur ne devient public au plugin.
BRIDGE int Python_BindImports(void* handle, const char* contract, void* callback) noexcept {
    try {
        return Dispatch([=] {
            Gil gil;
            Object args(Checked(Api.PyTuple_New(3)));
            std::ostringstream callbackText, handleText;
            callbackText << std::hex << reinterpret_cast<std::uintptr_t>(callback);
            handleText << std::hex << reinterpret_cast<std::uintptr_t>(handle);
            Object name(Checked(Api.PyUnicode_FromString(contract)));
            Object address(Checked(Api.PyUnicode_FromString(callbackText.str().c_str())));
            Object owner(Checked(Api.PyUnicode_FromString(handleText.str().c_str())));
            SetItem(args.Value, 0, name); SetItem(args.Value, 1, address); SetItem(args.Value, 2, owner);
            Call(static_cast<Plugin*>(handle)->Instance.Value, "bind", args.Value);
            return 1;
        });
    } catch (const std::exception& error) { Error = error.what(); return 0; }
}

namespace {
    void* OMNILINGUA_CALL CreatePlugin(const OmnilinguaPluginOptions* options) noexcept {
        if (!options) { Error = "Options absentes."; return nullptr; }
        return Python_Open(options->RuntimeLibrary, options->RuntimeHome, options->RuntimeExecutable, options->Sdk, options->Assembly, options->EntryPoint);
    }
    std::int32_t OMNILINGUA_CALL InitPlugin(void* handle, const char* config) noexcept { return Python_Lifecycle(handle, "init", config); }
    std::int32_t OMNILINGUA_CALL StartPlugin(void* handle) noexcept { return Python_Lifecycle(handle, "start", nullptr); }
    std::int32_t OMNILINGUA_CALL StopPlugin(void* handle) noexcept { return Python_Lifecycle(handle, "stop", nullptr); }
    std::int32_t OMNILINGUA_CALL DestroyPlugin(void* handle) noexcept { Error.clear(); Python_Close(handle); return Error.empty() ? 1 : 0; }
    const char* OMNILINGUA_CALL PluginError(void*) noexcept { return Error.c_str(); }
    std::int32_t OMNILINGUA_CALL InvokePlugin(void* handle, const char* contract, const char* method, const char** args, std::int32_t count) noexcept {
        int index = Python_ExportIndex(handle, contract, method);
        if (index < 0) { Error = "Export absent."; return 0; }
        return Python_Invoke(handle, index, args, count);
    }
}
PLUGIN_EXPORT const OmnilinguaPluginApi* OMNILINGUA_CALL OmnilinguaPlugin_GetApi() noexcept {
    static const OmnilinguaPluginApi api{OmnilinguaPluginDynamicCalls | OmnilinguaPluginCallbackImports | OmnilinguaPluginProcessLifetime, PY_MAJOR_VERSION * 100 + PY_MINOR_VERSION,
        CreatePlugin, Python_Metadata, InitPlugin, StartPlugin, StopPlugin, nullptr, nullptr, DestroyPlugin, PluginError, InvokePlugin, Python_BindImports};
    return &api;
}
