from omnilingua_python import IPlugin, PluginImport, PluginContext
from .logger_contract import ILogger

@PluginImport(ILogger)
class Plugin(IPlugin):
    def init(self, context: PluginContext) -> None:
        self._logger = context.services.get_required_service(ILogger)

    def start(self) -> None:
        self._logger.info("Hello.Python", "Bonjour depuis le plugin Python ! été🙂")
