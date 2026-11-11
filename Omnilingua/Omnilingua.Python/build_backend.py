"""Construit une wheel PEP 517 du SDK sans dépendance de construction externe."""
import base64
import csv
import hashlib
import io
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED


def build_wheel(wheel_directory, config_settings=None, metadata_directory=None):
    """Produit les modules du SDK et leurs métadonnées de distribution vérifiables."""
    root = Path(__file__).resolve().parent
    name = "omnilingua_python-9.9.9-py3-none-any.whl"
    info = "omnilingua_python-9.9.9.dist-info"
    files = {str(path.relative_to(root / "src")).replace("\\", "/"): path.read_bytes()
             for path in (root / "src" / "omnilingua_python").glob("*.py")}
    files[f"{info}/METADATA"] = b"Metadata-Version: 2.1\nName: omnilingua-python\nVersion: 9.9.9\nRequires-Python: >=3.10\n\n"
    files[f"{info}/WHEEL"] = b"Wheel-Version: 1.0\nGenerator: Omnilingua\nRoot-Is-Purelib: true\nTag: py3-none-any\n"
    for guide in (root.parents[1] / "docs").glob("*.md"):
        files[f"{info}/docs/{guide.name}"] = guide.read_bytes()
    site = root.parents[1] / "docs/.vitepress/dist"
    for asset in site.rglob("*"):
        if asset.is_file():
            files[f"{info}/docs/site/{asset.relative_to(site).as_posix()}"] = asset.read_bytes()
    record = io.StringIO(newline="")
    writer = csv.writer(record)
    for path, data in sorted(files.items()):
        digest = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode("ascii")
        writer.writerow((path, "sha256=" + digest, len(data)))
    writer.writerow((f"{info}/RECORD", "", ""))
    files[f"{info}/RECORD"] = record.getvalue().encode("utf-8")
    with ZipFile(Path(wheel_directory) / name, "w", ZIP_DEFLATED) as wheel:
        for path, data in sorted(files.items()):
            wheel.writestr(path, data)
    return name
