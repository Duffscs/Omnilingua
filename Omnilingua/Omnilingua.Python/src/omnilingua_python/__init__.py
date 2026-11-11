"""Déclarations des contrats et cycle de vie des plugins Python."""

from dataclasses import dataclass
import re


class ServiceProvider:
    """Résout les interfaces locales annoncées par PluginImport."""

    def __init__(self, services):
        self._services = services

    def get_service(self, interface):
        return self._services.get(interface)

    def get_required_service(self, interface):
        service = self.get_service(interface)
        if service is None:
            raise LookupError(f"Service obligatoire absent : {interface.__name__}")
        return service


@dataclass(frozen=True)
class PluginContext:
    """Configuration JSON opaque et services souscrits par le plugin."""

    configuration: str
    services: ServiceProvider


class IPlugin:
    """Cycle de vie commun ; seules les opérations utiles sont à redéfinir."""

    def get_export(self) -> list[object]:
        return []

    def init(self, context: PluginContext) -> None:
        pass

    def start(self) -> None:
        pass

    def stop(self) -> None:
        pass

    def close(self) -> None:
        pass


def _declaration(key, interface, name, version, optional=False):
    if not isinstance(interface, type):
        raise ValueError("Une interface doit etre un type.")
    name = interface.__name__ if name is None else name
    if (not isinstance(interface, type) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_.]*", name)
            or type(version) is not int or not 1 <= version <= 0xFFFFFFFF or type(optional) is not bool):
        raise ValueError("Type, nom, version ou option de contrat invalide.")

    def decorate(plugin):
        declarations = list(plugin.__dict__.get(key, ()))
        declarations.append((interface, name, version, optional))
        setattr(plugin, key, declarations)
        return plugin

    return decorate


def PluginExport(interface, *, name=None, version=1):
    """Annonce une interface fournie par les objets de get_export()."""
    return _declaration("__omnilingua_exports__", interface, name, version)


def PluginImport(interface, *, name=None, version=1, optional=False):
    """Annonce une interface résolue dans le fournisseur configuré."""
    return _declaration("__omnilingua_imports__", interface, name, version, optional)


from .runtime import RuntimePlugin
