"""Vérifie les distributions réellement publiées et l'invalidation ciblée du cache."""
import base64
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import unittest
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
package_directory = os.environ.get("PROJECT_DIR_PACKAGE")
environment_file = ROOT / ".env"
if not package_directory and environment_file.exists():
    for line in environment_file.read_text(encoding="utf-8-sig").splitlines():
        key, separator, value = line.partition("=")
        if separator and key.strip() == "PROJECT_DIR_PACKAGE":
            package_directory = value.strip()
            break
PACKAGE_ROOT = (ROOT / (package_directory or "artifacts/packages")).resolve()
FEED = PACKAGE_ROOT / "local_nuget"


class PackageTests(unittest.TestCase):
    def test_product_distribution(self):
        configuration_name = os.environ.get("OMNILINGUA_TEST_CONFIGURATION", "Release")
        product = ROOT / f"artifacts/Omnilingua.Runtime-9.9.9-win-x64-{configuration_name}.zip"
        with ZipFile(product) as archive:
            names = {name.replace("\\", "/") for name in archive.namelist()}
            for required in ("Omnilingua.exe", "Omnilingua.dll", "Omnilingua.Dotnet.dll", "Omnilingua.Java.Bridge.dll", "Omnilingua.Python.Bridge.dll", "Logger.Dotnet.dll"):
                self.assertIn(required, names)
            self.assertIn("Hello.Cpp.dll", names)
            self.assertIn("plugins.hello.cpp.json", names)
            configuration = json.loads(archive.read("Omnilingua.runtimeconfig.json"))
            frameworks = configuration["runtimeOptions"]["frameworks"]
            self.assertTrue(any(framework["name"] == "Microsoft.AspNetCore.App" for framework in frameworks))

    def test_nuget_contents(self):
        expected = {
            "Omnilingua.Dotnet": "lib/net10.0/Omnilingua.Dotnet.dll",
            "Omnilingua.Dotnet.SourceGen": "analyzers/dotnet/cs/Omnilingua.Dotnet.SourceGen.dll",
            "Omnilingua.Cpp": "build/native/include/Omnilingua/PluginMetadata.h",
            "Omnilingua.Java.Bridge": "runtimes/win-x64/native/Omnilingua.Java.Bridge.dll",
            "Omnilingua.Python.Bridge": "runtimes/win-x64/native/Omnilingua.Python.Bridge.dll",
            "Omnilingua.Runtime": "lib/net10.0/Omnilingua.dll",
            "Logger.Dotnet": "lib/net10.0/Logger.Dotnet.dll",
        }
        for package, asset in expected.items():
            with self.subTest(package=package), ZipFile(FEED / f"{package}.9.9.9.nupkg") as archive:
                self.assertIn(asset, archive.namelist())
                nuspec = ET.fromstring(archive.read(f"{package}.nuspec"))
                self.assertEqual(nuspec.find(".//{*}version").text, "9.9.9")
        with ZipFile(FEED / "Omnilingua.Cpp.9.9.9.nupkg") as archive:
            self.assertIn("build/native/OmnilinguaCppConfig.cmake", archive.namelist())
            self.assertIn("build/native/cmake/OmnilinguaCppGenerate.cmake", archive.namelist())
            self.assertIn("build/native/Omnilingua.Cpp.props", archive.namelist())
            self.assertIn("build/native/include/Omnilingua/Plugin.h", archive.namelist())
            self.assertIn("build/native/include/Omnilingua/Services.h", archive.namelist())
            self.assertIn("build/native/generator/generate_contract.py", archive.namelist())

    def test_python_wheel_integrity(self):
        with ZipFile(PACKAGE_ROOT / "local_pypi/omnilingua_python-9.9.9-py3-none-any.whl") as archive:
            self.assertIn("omnilingua_python/runtime.py", archive.namelist())
            record = archive.read("omnilingua_python-9.9.9.dist-info/RECORD").decode()
            for path, digest, size in csv.reader(io.StringIO(record)):
                if not digest:
                    continue
                data = archive.read(path)
                actual = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode()
                self.assertEqual(digest, "sha256=" + actual)
                self.assertEqual(int(size), len(data))

    def test_maven_artifact(self):
        artifact = PACKAGE_ROOT / "local_maven/io/omnilingua/omnilingua-java/9.9.9/omnilingua-java-9.9.9"
        pom = ET.parse(str(artifact) + ".pom")
        self.assertEqual(pom.find(".//{*}groupId").text, "io.omnilingua")
        self.assertEqual(pom.find(".//{*}version").text, "9.9.9")
        with ZipFile(str(artifact) + ".jar") as archive:
            self.assertIn("omnilingua/java/PluginExport.class", archive.namelist())
            self.assertIn("omnilingua/java/PluginImport.class", archive.namelist())
            self.assertNotIn("omnilingua/java/ExportContract.class", archive.namelist())

    def test_cache_invalidation_is_scoped(self):
        cache = ROOT / ".tmp" / ("package-cache-" + uuid.uuid4().hex)
        for relative in ("omnilingua.dotnet/9.9.9", "omnilingua.dotnet/9.9.8", "other.package/9.9.9"):
            (cache / relative).mkdir(parents=True)
            (cache / relative / "sentinel").write_text("conserver sauf version cible", encoding="utf-8")
        environment = dict(os.environ, NUGET_PACKAGES=str(cache))
        subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                        str(ROOT / "build/Publish-LocalPackage.ps1"),
                        "-Kind", "Nuget", "-PackageId", "Omnilingua.Dotnet"], env=environment, check=True, capture_output=True)
        self.assertFalse((cache / "omnilingua.dotnet/9.9.9").exists())
        self.assertTrue((cache / "omnilingua.dotnet/9.9.8/sentinel").exists())
        self.assertTrue((cache / "other.package/9.9.9/sentinel").exists())


if __name__ == "__main__":
    unittest.main()
