"""Check the Web artifact before storage or optional Cloudflare Pages upload."""

import argparse
from pathlib import Path


def verify_build(directory: Path, pages_limit: bool = False) -> None:
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
    if pages_limit:
        oversized = [str(file) for file in directory.rglob("*")
                     if file.is_file() and file.stat().st_size > 25 * 1024 * 1024]
        if oversized:
            raise ValueError(f"Cloudflare Pages asset limit exceeded (25 MiB): {oversized}")
    print(f"Web build artifact verified: {directory}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--pages-limit", action="store_true")
    args = parser.parse_args()
    verify_build(args.directory, args.pages_limit)
