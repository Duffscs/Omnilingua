from omnilingua_python import IPlugin, PluginExport
from .logger_contract import ILogger
from .logger import Logger

@PluginExport(ILogger)
class Plugin(IPlugin):
    def __init__(self):
        self._logger = Logger()

    def get_export(self) -> list[object]:
        return [self._logger]
