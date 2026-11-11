"""Adaptateur interne appelé par le pont CPython, indépendant des contrats métier."""

import importlib.util
import inspect
import sys
import typing
import uuid
from pathlib import Path

from . import IPlugin, PluginContext, ServiceProvider
import ctypes


class RuntimePlugin:
    def __init__(self, file: str, entry: str):
        self._name = "_omnilingua_plugin_" + uuid.uuid4().hex
        self._plugin = None
        self._operations = []
        self._services = {}
        self._closed = False
        try:
            path = Path(file).resolve(strict=True)
            spec = importlib.util.spec_from_file_location(
                self._name, path, submodule_search_locations=[str(path.parent)]
            )
            if spec is None or spec.loader is None:
                raise ImportError(f"Module Python impossible à charger : {path}")
            module = importlib.util.module_from_spec(spec)
            sys.modules[self._name] = module
            spec.loader.exec_module(module)
            self._plugin = getattr(module, entry)()
            if not isinstance(self._plugin, IPlugin):
                raise TypeError(f"{entry} doit implémenter omnilingua_python.IPlugin.")
            self._exports = self._declarations("__omnilingua_exports__")
            self._imports = self._declarations("__omnilingua_imports__")
            if len({item[0] for item in self._imports}) != len(self._imports):
                raise ValueError("Interface importée plusieurs fois.")
            for interface, contract, version, _ in self._exports:
                for name, count in self._methods(interface, contract):
                    self._operations.append((contract, version, name, count, None, interface))
        except BaseException as error:
            try:
                self.close()
            except BaseException as closing:
                error.add_note(f"Erreur pendant la fermeture partielle : {closing}")
            raise

    @staticmethod
    def load(file: str, entry: str):
        return RuntimePlugin(file, entry)

    def _declarations(self, key):
        declarations = type(self._plugin).__dict__.get(key, ())
        if len({item[1] for item in declarations}) != len(declarations):
            raise ValueError("Contrat dupliqué.")
        for interface, name, _, _ in declarations:
            self._methods(interface, name)
        return declarations

    @staticmethod
    def _methods(interface, contract):
        methods = []
        for name, method in inspect.getmembers(interface, inspect.isfunction):
            if name.startswith("_"):
                continue
            if inspect.iscoroutinefunction(method):
                raise TypeError(f"{contract}.{name} : les méthodes async ne sont pas prises en charge.")
            parameters = list(inspect.signature(method).parameters.values())
            hints = typing.get_type_hints(method)
            if (not parameters or parameters[0].name != "self" or hints.get("return") is not type(None)
                    or len(parameters) > 17 or any(
                        parameter.kind not in (parameter.POSITIONAL_ONLY, parameter.POSITIONAL_OR_KEYWORD)
                        or hints.get(parameter.name) is not str for parameter in parameters[1:])):
                raise TypeError(f"{contract}.{name} : seuls -> None et les paramètres str sont pris en charge.")
            methods.append((name, len(parameters) - 1))
        if not methods:
            raise TypeError(f"Contrat vide : {contract}")
        return methods

    def describe(self):
        return [f"{contract}\t{version}\t{name}\t{count}" for contract, version, name, count, _, _ in self._operations]

    def describe_imports(self):
        return [f"{contract}\t{version}\t{name}\t{count}\t{int(optional)}"
                for interface, contract, version, optional in self._imports
                for name, count in self._methods(interface, contract)]

    def bind(self, contract, callback_address, handle_address):
        """Crée une implémentation locale ; les adresses restent privées au SDK."""
        if self._closed:
            raise RuntimeError("Plugin fermé.")
        interface = next(item[0] for item in self._imports if item[1] == contract)
        if interface in self._services:
            raise ValueError(f"Service déjà injecté : {contract}")
        callback_type = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_char_p,
                                        ctypes.c_char_p, ctypes.POINTER(ctypes.c_char_p), ctypes.c_int)
        callback = callback_type(int(callback_address, 16))
        handle = int(handle_address, 16)

        def operation(name, count):
            def invoke(proxy, *arguments):
                if self._closed:
                    raise RuntimeError("Plugin fermé.")
                if len(arguments) != count or any(not isinstance(value, str) or "\0" in value or len(value) > 32768 for value in arguments):
                    raise ValueError("Arguments str invalides, trop longs ou contenant un caractère nul.")
                values = (ctypes.c_char_p * count)(*(value.encode("utf-8") for value in arguments))
                if callback(handle, contract.encode("utf-8"), name.encode("utf-8"), values, count) != 1:
                    raise RuntimeError(f"Échec de l'import : {contract}.{name}")
            return invoke

        proxy_type = type("Imported" + interface.__name__, (interface,),
                          {name: operation(name, count) for name, count in self._methods(interface, contract)})
        self._services[interface] = proxy_type()

    def invoke(self, index: int, arguments: tuple):
        target = self._operations[index][4]
        if target is None or self._closed:
            raise RuntimeError("Plugin non initialisé ou fermé.")
        result = target(*arguments)
        if result is not None:
            raise TypeError("Un export déclaré -> None a retourné une valeur.")

    def init(self, configuration: str):
        for interface, contract, _, optional in self._imports:
            if not optional and interface not in self._services:
                raise LookupError(f"Import obligatoire absent : {contract}")
        self._plugin.init(PluginContext(configuration, ServiceProvider(self._services)))
        targets = self._plugin.get_export()
        if not isinstance(targets, (list, tuple)) or any(
                not any(isinstance(target, item[0]) for item in self._exports) for target in targets):
            raise TypeError("Objet exporté sans contrat annoncé.")
        for index, (contract, version, name, count, _, interface) in enumerate(self._operations):
            matches = [target for target in targets if isinstance(target, interface)]
            if len(matches) != 1:
                raise ValueError(f"Une implémentation unique est requise pour {contract}.")
            implementation = getattr(matches[0], name)
            if inspect.iscoroutinefunction(implementation):
                raise TypeError(f"{contract}.{name} : implémentation async non prise en charge.")
            inspect.signature(implementation).bind(*[""] * count)
            self._operations[index] = (contract, version, name, count, implementation, interface)

    def start(self):
        self._plugin.start()

    def stop(self):
        self._plugin.stop()

    def close(self):
        try:
            if self._plugin is not None:
                self._plugin.close()
        finally:
            self._closed = True
            self._services.clear()
            self._operations.clear()
            self._plugin = None
            for name in list(sys.modules):
                if name == self._name or name.startswith(self._name + "."):
                    del sys.modules[name]
