"""Queue AI chats that share one Unity Editor so recompiles and test runs do not overlap.

The holder gets a token from acquire and returns it with release. A lock whose lease has
ended is taken over by the next waiter, so a chat that stops halfway cannot block the rest.
"""

import argparse
import json
import os
from pathlib import Path
import secrets
import sys
import time


LOCK_FILE = Path(__file__).resolve().parents[1] / "Temp" / "editor-lock.json"
POLL_SECONDS = 2
DEFAULT_WAIT_SECONDS = 90
DEFAULT_LEASE_SECONDS = 600
EXIT_BUSY = 3
EXIT_NOT_HELD = 4


class Busy(Exception):
    def __init__(self, lock):
        super().__init__(lock["owner"])
        self.lock = lock


class NotHeld(Exception):
    pass


def read(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return None
    except (OSError, ValueError):
        return {"owner": "(unreadable lock file)", "token": "", "acquired": 0, "expires": 0}


def write_new(path, lock):
    """Create the lock file with its full content, or return False if another holder exists."""
    path.parent.mkdir(parents=True, exist_ok=True)
    draft = path.with_name(f"{path.name}.{lock['token']}.tmp")
    draft.write_text(json.dumps(lock, ensure_ascii=False), encoding="utf-8")
    try:
        os.link(draft, path)
        return True
    except FileExistsError:
        return False
    finally:
        draft.unlink()


def remove_if(path, should_remove):
    """Move the lock aside atomically and delete it only if it still matches; otherwise put it back."""
    aside = path.with_name(f"{path.name}.{secrets.token_hex(4)}.aside")
    try:
        os.rename(path, aside)
    except (FileNotFoundError, PermissionError):
        return False
    try:
        if should_remove(read(aside)):
            return True
        try:
            os.link(aside, path)
        except FileExistsError:
            pass
        return False
    finally:
        aside.unlink()


def acquire(path, owner, wait, lease, now=time.time, sleep=time.sleep):
    token = secrets.token_hex(4)
    deadline = now() + wait
    while True:
        current = now()
        if write_new(path, {"owner": owner, "token": token, "acquired": current, "expires": current + lease}):
            return token
        held = read(path)
        if held is None or (
            held["expires"] <= current and remove_if(path, lambda lock: lock["expires"] <= current)
        ):
            continue
        if current >= deadline:
            raise Busy(held)
        sleep(POLL_SECONDS)


def renew(path, token, lease, now=time.time):
    current = now()
    held = read(path)
    if held is None or held["token"] != token or held["expires"] <= current:
        raise NotHeld()
    held["expires"] = current + lease
    draft = path.with_name(f"{path.name}.{token}.tmp")
    draft.write_text(json.dumps(held, ensure_ascii=False), encoding="utf-8")
    os.replace(draft, path)


def release(path, token):
    held = read(path)
    if held is None or held["token"] != token or not remove_if(path, lambda lock: lock["token"] == token):
        raise NotHeld()


def describe(lock, current):
    return (
        f'"{lock["owner"]}" holds the Unity Editor '
        f"({current - lock['acquired']:.0f}s, lease ends in {lock['expires'] - current:.0f}s)"
    )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock-file", type=Path, default=LOCK_FILE, help=argparse.SUPPRESS)
    commands = parser.add_subparsers(dest="command", required=True)
    take = commands.add_parser("acquire", help="wait for the Editor and print a token")
    take.add_argument("--owner", required=True, help="what this chat is working on")
    take.add_argument("--wait", type=float, default=DEFAULT_WAIT_SECONDS, help="seconds to wait before giving up")
    take.add_argument("--lease", type=float, default=DEFAULT_LEASE_SECONDS, help="seconds until others may take over")
    extend = commands.add_parser("renew", help="extend the lease of a held token")
    extend.add_argument("token")
    extend.add_argument("--lease", type=float, default=DEFAULT_LEASE_SECONDS)
    give = commands.add_parser("release", help="return a held token")
    give.add_argument("token")
    commands.add_parser("status", help="show who holds the Editor")
    args = parser.parse_args(argv)

    try:
        if args.command == "acquire":
            token = acquire(args.lock_file, args.owner, args.wait, args.lease)
            print(f"acquired token={token} lease={args.lease:.0f}s")
        elif args.command == "renew":
            renew(args.lock_file, args.token, args.lease)
            print(f"renewed token={args.token} lease={args.lease:.0f}s")
        elif args.command == "release":
            release(args.lock_file, args.token)
            print(f"released token={args.token}")
        else:
            held = read(args.lock_file)
            current = time.time()
            if held is None or held["expires"] <= current:
                print("free")
            else:
                print(describe(held, current))
    except Busy as busy:
        print(f"busy: {describe(busy.lock, time.time())}; run acquire again to keep waiting")
        return EXIT_BUSY
    except NotHeld:
        print(f"not held: token {args.token} no longer holds the lock (released, expired, or taken over)")
        return EXIT_NOT_HELD
    return 0


if __name__ == "__main__":
    sys.exit(main())
