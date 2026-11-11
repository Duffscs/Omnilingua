"""Vérifie les sorties du logger et les invariants du SDK sans dépendance externe."""

import contextlib
import ctypes
import io
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Omnilingua/Omnilingua.Python/src"))
from omnilingua_python import RuntimePlugin, PluginExport, PluginImport


class PythonSdkTests(unittest.TestCase):
    def load_code(self, code):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        file = Path(directory.name) / "plugin.py"
        file.write_text(code, encoding="utf-8")
        plugin = RuntimePlugin.load(str(file), "Plugin")
        self.addCleanup(plugin.close)
        return plugin

    def test_import_transparent_copie_unicode_et_proxy_invalide_apres_fermeture(self):
        plugin = RuntimePlugin.load(str(ROOT / "SamplePlugins/Hello/Python/plugin.py"), "Plugin")
        self.addCleanup(plugin.close)
        self.assertEqual(plugin.describe(), [])
        self.assertEqual(plugin.describe_imports(), ["ILogger\t1\tinfo\t2\t0"])
        received = []
        callback_type = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_char_p,
                                        ctypes.c_char_p, ctypes.POINTER(ctypes.c_char_p), ctypes.c_int)

        @callback_type
        def callback(handle, contract, method, arguments, count):
            received.append((handle, contract.decode(), method.decode(),
                             [arguments[index].decode() for index in range(count)]))
            return 1

        plugin.bind("ILogger", format(ctypes.cast(callback, ctypes.c_void_p).value, "x"), "42")
        plugin.init("{}")
        plugin.start()
        proxy = plugin._plugin._logger
        proxy.info("été", "漢字🙂")
        self.assertEqual(received[-1], (0x42, "ILogger", "info", ["été", "漢字🙂"]))
        with self.assertRaises(ValueError):
            proxy.info("contexte", "nul\0interdit")
        plugin.close()
        with self.assertRaises(RuntimeError):
            proxy.info("contexte", "message")

    def test_import_obligatoire_absent_et_optionnel_absent(self):
        code = '''
from omnilingua_python import IPlugin, PluginImport
class IService:
    def info(self, message: str) -> None: pass
@PluginImport(IService, optional=OPTIONAL)
class Plugin(IPlugin):
    def init(self, context):
        assert context.services.get_service(IService) is None
'''
        required = self.load_code(code.replace("OPTIONAL", "False"))
        with self.assertRaises(LookupError):
            required.init("{}")
        optional = self.load_code(code.replace("OPTIONAL", "True"))
        self.assertTrue(optional.describe_imports()[0].endswith("\t1"))
        optional.init("{}")

    def test_exports_absents_ambigus_et_non_declares_refuses(self):
        code = '''
from omnilingua_python import IPlugin, PluginExport
class IService:
    def info(self, message: str) -> None: pass
class Service(IService): pass
@PluginExport(IService)
class Plugin(IPlugin):
    def get_export(self): return TARGETS
'''
        for targets in ("[]", "[Service(), Service()]", "[object()]"):
            with self.subTest(targets=targets):
                plugin = self.load_code(code.replace("TARGETS", targets))
                with self.assertRaises((ValueError, TypeError)):
                    plugin.init("{}")

    def test_declarations_dupliquees_refusees(self):
        code = '''
from omnilingua_python import IPlugin, PluginImport
class IService:
    def info(self, message: str) -> None: pass
@PluginImport(IService)
@PluginImport(IService)
class Plugin(IPlugin): pass
'''
        with self.assertRaises(ValueError):
            self.load_code(code)

    def test_logger_identifie_python_et_preserve_le_message(self):
        plugin = RuntimePlugin.load(str(ROOT / "SamplePlugins/Logger/Python/plugin.py"), "Plugin")
        self.addCleanup(plugin.close)
        plugin.init("{}")
        operations = plugin.describe()
        self.assertEqual(len(operations), 5)
        for index, description in enumerate(operations):
            contract, version, name, count = description.split("\t")
            self.assertEqual((contract, version, count), ("ILogger", "1", "2"))
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                plugin.invoke(index, ("MonPlugin", "abc🙂 été"))
            self.assertTrue(output.getvalue().startswith("[LoggerPython] "))
            self.assertIn(f"[{name.upper()}] [MonPlugin] abc🙂 été\n", output.getvalue())

    def test_deux_chargements_isolent_les_modules_et_les_liberent(self):
        code = '''
from omnilingua_python import IPlugin, PluginExport
counter = 0
class IService:
    def info(self, context: str, message: str) -> None: pass
@PluginExport(IService, name="ITest")
class Plugin(IPlugin, IService):
    def get_export(self): return [self]
    def info(self, context, message):
        global counter
        counter += 1
        print(counter)
'''
        before = {name for name in sys.modules if name.startswith("_omnilingua_plugin_")}
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / "plugin.py"
            file.write_text(code, encoding="utf-8")
            first = RuntimePlugin.load(str(file), "Plugin")
            second = RuntimePlugin.load(str(file), "Plugin")
            try:
                first.init("{}")
                second.init("{}")
                output = io.StringIO()
                with contextlib.redirect_stdout(output):
                    first.invoke(0, ("", ""))
                    second.invoke(0, ("", ""))
                self.assertEqual(output.getvalue(), "1\n1\n")
            finally:
                first.close()
                second.close()
        self.assertEqual(before, {name for name in sys.modules if name.startswith("_omnilingua_plugin_")})

    def test_signature_incompatible_et_echec_de_fermeture_preservent_erreur_initiale(self):
        code = '''
from omnilingua_python import IPlugin, PluginExport
class IService:
    def info(self, context: str, message: int) -> None: pass
@PluginExport(IService, name="ITest")
class Plugin(IPlugin, IService):
    def get_export(self): return [self]
    def close(self): raise RuntimeError("fermeture de test")
'''
        before = {name for name in sys.modules if name.startswith("_omnilingua_plugin_")}
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / "plugin.py"
            file.write_text(code, encoding="utf-8")
            with self.assertRaisesRegex(TypeError, "paramètres str") as error:
                RuntimePlugin.load(str(file), "Plugin")
            self.assertIn("fermeture de test", " ".join(error.exception.__notes__))
        self.assertEqual(before, {name for name in sys.modules if name.startswith("_omnilingua_plugin_")})

    def test_async_refuse_avant_invocation(self):
        code = '''
from omnilingua_python import IPlugin, PluginExport
class IService:
    async def info(self, context: str, message: str) -> None: pass
@PluginExport(IService, name="ITest")
class Plugin(IPlugin, IService):
    def get_export(self): return [self]
'''
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / "plugin.py"
            file.write_text(code, encoding="utf-8")
            with self.assertRaisesRegex(TypeError, "async"):
                RuntimePlugin.load(str(file), "Plugin")

    def test_contrat_invalide_refuse(self):
        for name, version in [("invalide\t", 1), ("ITest", 0), ("ITest", True), ("ITest", 2.5)]:
            with self.assertRaises(ValueError):
                PluginExport(type("ITest", (), {}), name=name, version=version)


if __name__ == "__main__":
    unittest.main()
