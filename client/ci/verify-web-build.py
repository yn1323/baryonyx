"""Check the Web artifact before storage or Cloudflare Static Assets upload."""

import argparse
from pathlib import Path


def verify_build(directory: Path, cloudflare_limit: bool = False) -> None:
    if not (directory / "index.html").is_file():
        raise ValueError("Web build index.html is missing")
    files = list((directory / "Build").glob("*"))
    for extension in (".loader.js", ".framework.js", ".data", ".wasm"):
        suffixes = (extension,) if extension == ".loader.js" else tuple(
            extension + suffix for suffix in ("", ".unityweb", ".gz", ".br")
        )
        matches = [file for file in files if file.is_file() and file.name.endswith(suffixes)]
        if not matches or any(file.stat().st_size == 0 for file in matches):
            raise ValueError(f"Web build has missing or empty {extension}")
    if cloudflare_limit:
        oversized = [str(file) for file in directory.rglob("*")
                     if file.is_file() and file.stat().st_size > 25 * 1024 * 1024]
        if oversized:
            raise ValueError(f"Cloudflare Static Assets limit exceeded (25 MiB): {oversized}")
    print(f"Web build artifact verified: {directory}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--cloudflare-limit", "--pages-limit", dest="cloudflare_limit",
                        action="store_true", help="Enforce the 25 MiB limit per static asset")
    args = parser.parse_args()
    verify_build(args.directory, args.cloudflare_limit)
