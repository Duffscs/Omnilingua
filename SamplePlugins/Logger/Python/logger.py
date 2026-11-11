from datetime import datetime, timezone
from threading import Lock
from .logger_contract import ILogger

class Logger(ILogger):
    _gate = Lock()

    @classmethod
    def _write(cls, level: str, context: str, message: str) -> None:
        with cls._gate:
            print(f"[LoggerPython] {datetime.now(timezone.utc).isoformat()} [{level}] [{context}] {message}", flush=True)

    def debug(self, context: str, message: str) -> None:
        self._write("DEBUG", context, message)

    def info(self, context: str, message: str) -> None:
        self._write("INFO", context, message)

    def warn(self, context: str, message: str) -> None:
        self._write("WARN", context, message)

    def error(self, context: str, message: str) -> None:
        self._write("ERROR", context, message)

    def fatal(self, context: str, message: str) -> None:
        self._write("FATAL", context, message)
