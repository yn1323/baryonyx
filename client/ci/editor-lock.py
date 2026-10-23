"""Queue AI chats that share one Unity Editor so recompiles and test runs do not overlap.

The holder gets a token from acquire and returns it with release. Waiters take the lock in the
order they started waiting. A lock whose lease has ended is taken over by the next waiter, and no
holder keeps it past MAX_HOLD_SECONDS, so a chat that stops halfway cannot block the rest.
A Claude Code hook runs release-session when a chat ends its turn, returning a lock it still holds.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import sys
import time


LOCK_FILE = Path(__file__).resolve().parents[1] / "Temp" / "editor-lock.json"
POLL_SECONDS = 2
DEFAULT_WAIT_SECONDS = 90
DEFAULT_LEASE_SECONDS = 300
MAX_HOLD_SECONDS = 900
TICKET_GRACE_SECONDS = 30
SESSION_VARIABLE = "CLAUDE_CODE_SESSION_ID"
EXIT_BUSY = 3
EXIT_NOT_HELD = 4
KEEP = object()


class Busy(Exception):
    def __init__(self, lock, ahead):
        super().__init__(lock["owner"] if lock else ", ".join(ahead))
        self.lock = lock
        self.ahead = ahead


class NotHeld(Exception):
    pass


def read(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return None
    except (OSError, ValueError):
        return {"owner": "(unreadable lock file)", "token": "", "acquired": 0, "expires": 0}


def write_over(path, content):
    draft = path.with_name(f"{path.name}.{secrets.token_hex(4)}.tmp")
    draft.write_text(json.dumps(content, ensure_ascii=False), encoding="utf-8")
    os.replace(draft, path)


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


def ticket_path(path, key):
    return path.with_name(f"{path.name}.{hashlib.sha256(key.encode()).hexdigest()[:16]}.ticket")


def waiting(path, current):
    """Return the live waiters' tickets in line order, dropping those that stopped checking in."""
    tickets = []
    for ticket in path.parent.glob(f"{path.name}.*.ticket"):
        content = read(ticket)
        if content is None:
            continue
        if content.get("seen", 0) <= current - TICKET_GRACE_SECONDS:
            ticket.unlink(missing_ok=True)
            continue
        tickets.append((content.get("since", 0), ticket.name, content))
    return [(name, content) for _, name, content in sorted(tickets, key=lambda entry: entry[:2])]


def check_in(path, key, owner, current):
    """Keep this waiter's place in line and return the owners waiting ahead of it."""
    path.parent.mkdir(parents=True, exist_ok=True)
    mine = ticket_path(path, key)
    ticket = read(mine)
    write_over(mine, {"owner": owner, "since": ticket["since"] if ticket else current, "seen": current})
    ahead = []
    for name, content in waiting(path, current):
        if name == mine.name:
            return ahead
        ahead.append(content["owner"])
    return ahead


def leave(path, key):
    ticket_path(path, key).unlink(missing_ok=True)


def acquire(path, owner, wait, lease, session=None, pid=None, now=time.time, sleep=time.sleep):
    token = secrets.token_hex(4)
    key = session or owner
    deadline = now() + wait
    while True:
        current = now()
        ahead = check_in(path, key, owner, current)
        lock = {
            "owner": owner,
            "token": token,
            "session": session,
            "pid": pid,
            "acquired": current,
            "expires": current + min(lease, MAX_HOLD_SECONDS),
        }
        if not ahead and write_new(path, lock):
            leave(path, key)
            return token
        held = read(path)
        if held is not None and held["expires"] <= current and remove_if(
            path, lambda lock: lock["expires"] <= current
        ):
            continue
        if current >= deadline:
            raise Busy(held, ahead)
        sleep(POLL_SECONDS)


def renew(path, token, lease, now=time.time, pid=KEEP):
    """Extend the lease, but never past MAX_HOLD_SECONDS from when the lock was taken."""
    current = now()
    held = read(path)
    if held is None or held["token"] != token or held["expires"] <= current:
        raise NotHeld()
    held["expires"] = min(current + lease, held["acquired"] + MAX_HOLD_SECONDS)
    if pid is not KEEP:
        held["pid"] = pid
    write_over(path, held)
    return held["expires"] - current


def release(path, token):
    held = read(path)
    if held is None or held["token"] != token or not remove_if(path, lambda lock: lock["token"] == token):
        raise NotHeld()


def alive(pid):
    if not pid:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def release_session(path, session):
    """Return the lock and the place in line of a chat that ended its turn, unless a running script holds it."""
    leave(path, session)
    held = read(path)
    if held is None or held.get("session") != session or alive(held.get("pid")):
        return False
    return remove_if(path, lambda lock: lock.get("token") == held["token"])


def describe(lock, ahead, current, waiters="waiting ahead"):
    parts = []
    if lock:
        parts.append(
            f'"{lock["owner"]}" holds the Unity Editor '
            f"({current - lock['acquired']:.0f}s, lease ends in {lock['expires'] - current:.0f}s)"
        )
    if ahead:
        parts.append(f"{waiters}: " + ", ".join(f'"{owner}"' for owner in ahead))
    return "; ".join(parts)


def hook_session():
    """Read the session id from a Claude Code hook's JSON on stdin, or from the environment."""
    if not sys.stdin.isatty():
        try:
            session = json.loads(sys.stdin.read() or "{}").get("session_id")
            if session:
                return session
        except ValueError:
            pass
    return os.environ.get(SESSION_VARIABLE)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock-file", type=Path, default=LOCK_FILE, help=argparse.SUPPRESS)
    commands = parser.add_subparsers(dest="command", required=True)
    take = commands.add_parser("acquire", help="wait for the Editor and print a token")
    take.add_argument("--owner", required=True, help="what this chat is working on")
    take.add_argument("--wait", type=float, default=DEFAULT_WAIT_SECONDS, help="seconds to wait before giving up")
    take.add_argument("--lease", type=float, default=DEFAULT_LEASE_SECONDS, help="seconds until others may take over")
    take.add_argument("--session", default=os.environ.get(SESSION_VARIABLE), help=argparse.SUPPRESS)
    extend = commands.add_parser("renew", help="extend the lease of a held token")
    extend.add_argument("token")
    extend.add_argument("--lease", type=float, default=DEFAULT_LEASE_SECONDS)
    give = commands.add_parser("release", help="return a held token")
    give.add_argument("token")
    commands.add_parser("release-session", help="return the lock of a chat that ended its turn (Claude Code hook)")
    commands.add_parser("status", help="show who holds the Editor and who is waiting")
    args = parser.parse_args(argv)

    try:
        if args.command == "acquire":
            token = acquire(args.lock_file, args.owner, args.wait, args.lease, session=args.session)
            print(f"acquired token={token} lease={min(args.lease, MAX_HOLD_SECONDS):.0f}s")
        elif args.command == "renew":
            remaining = renew(args.lock_file, args.token, args.lease)
            if remaining < args.lease:
                print(
                    f"renewed token={args.token} lease={remaining:.0f}s (capped: the lock is held at most "
                    f"{MAX_HOLD_SECONDS}s; release it and acquire again to keep using the Editor)"
                )
            else:
                print(f"renewed token={args.token} lease={remaining:.0f}s")
        elif args.command == "release":
            release(args.lock_file, args.token)
            print(f"released token={args.token}")
        elif args.command == "release-session":
            # A hook must not fail the chat's turn, so problems with the lock files are ignored here.
            try:
                session = hook_session()
                if session and release_session(args.lock_file, session):
                    print("released the Unity Editor lock this chat still held")
            except OSError:
                pass
        else:
            current = time.time()
            held = read(args.lock_file)
            if held is not None and held["expires"] <= current:
                held = None
            ahead = [content["owner"] for _, content in waiting(args.lock_file, current)]
            state = describe(held, ahead, current, waiters="waiting")
            print(state if held else "; ".join(part for part in ("free", state) if part))
    except Busy as busy:
        print(f"busy: {describe(busy.lock, busy.ahead, time.time())}; run acquire again within "
              f"{TICKET_GRACE_SECONDS}s to keep your place in line")
        return EXIT_BUSY
    except NotHeld:
        print(f"not held: token {args.token} no longer holds the lock (released, expired, or taken over)")
        return EXIT_NOT_HELD
    return 0


if __name__ == "__main__":
    sys.exit(main())
