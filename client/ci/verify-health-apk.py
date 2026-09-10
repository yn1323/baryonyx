"""Health ConnectとUMothのクラス・権限が最終APKへ含まれることを確認する。"""

import sys
from pathlib import Path
from zipfile import ZipFile


def verify(path: Path) -> None:
    with ZipFile(path) as apk:
        dex = b"".join(apk.read(name) for name in apk.namelist() if name.endswith(".dex"))
        for name in (
            "com/baryonyx/health/HealthBridge",
            "com/baryonyx/health/HealthCallback",
            "com/baryonyx/health/HealthPermissionActivity",
            "com/baryonyx/health/HealthRationaleActivity",
            "com/uralstech/umoth/GoogleAuth",
        ):
            if f"L{name};".encode() not in dex:
                raise AssertionError(f"Missing Android class: {name}")
        manifest = apk.read("AndroidManifest.xml")

        def contains(permission: str) -> bool:
            return any(permission.encode(encoding) in manifest for encoding in ("utf-8", "utf-16le"))

        for required in ("android.permission.health.READ_STEPS", "android.permission.INTERNET"):
            if not contains(required):
                raise AssertionError(f"Missing permission: {required}")
        for forbidden in (
            "android.permission.health.WRITE_STEPS",
            "android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND",
            "android.permission.health.READ_HEALTH_DATA_HISTORY",
            "android.permission.FOREGROUND_SERVICE",
        ):
            if contains(forbidden):
                raise AssertionError(f"Unexpected permission: {forbidden}")
    print("Health APK: native classes and foreground read-only permissions OK")


if __name__ == "__main__":
    verify(Path(sys.argv[1]))
