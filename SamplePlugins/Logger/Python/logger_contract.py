class ILogger:
    def debug(self, context: str, message: str) -> None:
        raise NotImplementedError

    def info(self, context: str, message: str) -> None:
        raise NotImplementedError

    def warn(self, context: str, message: str) -> None:
        raise NotImplementedError

    def error(self, context: str, message: str) -> None:
        raise NotImplementedError

    def fatal(self, context: str, message: str) -> None:
        raise NotImplementedError
