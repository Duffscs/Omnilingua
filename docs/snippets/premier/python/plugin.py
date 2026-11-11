from omnilingua_python import IPlugin

class Plugin(IPlugin):
    def start(self) -> None:
        print("[Premier.Python] Le plugin a démarré !", flush=True)
