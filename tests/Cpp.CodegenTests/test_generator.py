"""Vérifie que le générateur refuse les déclarations qu’il ne sait pas traduire."""

import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("cpp_generator", ROOT / "Omnilingua/Omnilingua.Cpp/generator/generate_contract.py")
generator = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = generator
spec.loader.exec_module(generator)


class GeneratorTests(unittest.TestCase):
    def parse(self, declaration):
        with tempfile.TemporaryDirectory() as folder:
            header, output = Path(folder) / "Contract.h", Path(folder) / "Contract.g.h"
            header.write_text('#include <Omnilingua/Export.h>\n' + declaration, encoding="utf-8")
            arguments = ["-x", "c++", "-std=c++20", "-I" + str(ROOT / "Omnilingua/Omnilingua.Cpp/include")]
            if os.name == "nt":
                arguments += ["--target=x86_64-pc-windows-msvc"]
            generator.generate(header, output, arguments)
            return output.read_text(encoding="utf-8")

    def test_annotation_export_deduit_interface_non_annotee(self):
        generated = self.parse('''
namespace Omnilingua::Cpp { class IPlugin {}; }
namespace Example {
class ILogger : public virtual Omnilingua::Cpp::IService {
public: virtual void info(int value) = 0;
};
class OMNILINGUA_PLUGIN_EXPORT(ILogger) Plugin : public Omnilingua::Cpp::IPlugin {};
}
''')
        self.assertIn("PluginDeclaration<::Example::Plugin>", generated)
        self.assertIn("builder.DeclareExport<::Example::ILogger>()", generated)
        self.assertIn("builder.BindExport<::Example::ILogger>(objects)", generated)

    def test_import_optionnel_et_resolution_namespace_parent(self):
        generated = self.parse('''
namespace Omnilingua::Cpp { class IPlugin {}; }
namespace Example {
class ILogger : public virtual Omnilingua::Cpp::IService {
public: virtual void info(int value) = 0;
};
namespace Nested {
class OMNILINGUA_PLUGIN_IMPORT(ILogger, true) Plugin : public Omnilingua::Cpp::IPlugin {};
}}
''')
        self.assertIn("builder.Import<::Example::ILogger>(true)", generated)

    def test_declarations_plugin_invalides_refusees(self):
        preamble = '''
namespace Omnilingua::Cpp { class IPlugin {}; }
class ILogger : public virtual Omnilingua::Cpp::IService {
public: virtual void info(int value) = 0;
};
'''
        for declaration in (
            "class OMNILINGUA_PLUGIN_EXPORT(Unknown) Plugin : public Omnilingua::Cpp::IPlugin {};",
            "class OMNILINGUA_PLUGIN_EXPORT(ILogger) Plugin {};",
            "class OMNILINGUA_PLUGIN_EXPORT(ILogger) OMNILINGUA_PLUGIN_EXPORT(ILogger) Plugin : public Omnilingua::Cpp::IPlugin {};",
            "class OMNILINGUA_PLUGIN_IMPORT(ILogger) OMNILINGUA_PLUGIN_IMPORT(ILogger, true) Plugin : public Omnilingua::Cpp::IPlugin {};",
        ):
            with self.subTest(declaration=declaration), self.assertRaises(generator.GenerationError):
                self.parse(preamble + declaration)

    def test_types_non_supportes_refuses(self):
        with self.assertRaisesRegex(generator.GenerationError, "Type non pris en charge"):
            self.parse('class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { public: virtual double Value(double input) = 0; };')

    def test_surcharges_refusees(self):
        with self.assertRaisesRegex(generator.GenerationError, "surcharge"):
            self.parse('class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { public: virtual void Log(int value) = 0; virtual void Log(bool value) = 0; };')

    def test_erreur_de_syntaxe_refusee(self):
        with self.assertRaises(generator.GenerationError):
            self.parse('class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { ceci ne compile pas };')

    def test_interface_generique_refusee(self):
        with self.assertRaisesRegex(generator.GenerationError, "non générique"):
            self.parse('template<class T> class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { public: virtual void Value(T value) = 0; };')

    def test_champ_refuse(self):
        with self.assertRaisesRegex(generator.GenerationError, "uniquement des méthodes"):
            self.parse('class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { public: int Value; virtual void Log(int value) = 0; };')

    def test_nom_reserve_par_le_proxy_refuse(self):
        with self.assertRaisesRegex(generator.GenerationError, "réservé"):
            self.parse('class OMNILINGUA_PLUGIN_CONTRACT("ITest", 1) ITest { public: virtual void Inject() = 0; };')


if __name__ == "__main__":
    unittest.main()
