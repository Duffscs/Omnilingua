class ILogger:
    def info(self, context: str, message: str) -> None:
        raise NotImplementedError
