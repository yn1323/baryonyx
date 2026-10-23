"""Exercise the shared Unity Editor lock without starting Unity."""

import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


CI_DIRECTORY = Path(__file__).resolve().parent
SCRIPT = CI_DIRECTORY / "editor-lock.py"
spec = importlib.util.spec_from_file_location("editor_lock", SCRIPT)
editor_lock = importlib.util.module_from_spec(spec)
spec.loader.exec_module(editor_lock)


class Clock:
    def __init__(self):
        self.current = 1000.0
        self.on_sleep = None

    def now(self):
        return self.current

    def sleep(self, seconds):
        self.current += seconds
        if self.on_sleep:
            self.on_sleep()


class EditorLockTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-editor-lock-")
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.path = self.directory / "Temp" / "editor-lock.json"
        self.clock = Clock()

    def acquire(self, owner, wait=0, lease=600):
        return editor_lock.acquire(self.path, owner, wait, lease, now=self.clock.now, sleep=self.clock.sleep)

    def test_first_chat_takes_the_lock_and_the_next_waits_until_its_limit(self):
        token = self.acquire("home")
        self.assertEqual(editor_lock.read(self.path)["owner"], "home")
        with self.assertRaises(editor_lock.Busy) as raised:
            self.acquire("combat", wait=10)
        self.assertEqual(raised.exception.lock["token"], token)
        self.assertEqual(self.clock.current, 1010.0)

    def test_waiting_chat_gets_the_lock_once_the_holder_releases(self):
        token = self.acquire("home")
        self.clock.on_sleep = lambda: editor_lock.release(self.path, token)
        self.acquire("combat", wait=60)
        self.assertEqual(editor_lock.read(self.path)["owner"], "combat")

    def test_expired_lease_is_taken_over_and_its_old_token_is_rejected(self):
        old = self.acquire("home", lease=60)
        self.clock.current += 61
        self.acquire("combat")
        self.assertEqual(editor_lock.read(self.path)["owner"], "combat")
        with self.assertRaises(editor_lock.NotHeld):
            editor_lock.renew(self.path, old, 600, now=self.clock.now)
        with self.assertRaises(editor_lock.NotHeld):
            editor_lock.release(self.path, old)
        self.assertEqual(editor_lock.read(self.path)["owner"], "combat")

    def test_renew_keeps_the_lock_past_the_first_lease(self):
        token = self.acquire("home", lease=60)
        self.clock.current += 50
        editor_lock.renew(self.path, token, 600, now=self.clock.now)
        self.clock.current += 50
        with self.assertRaises(editor_lock.Busy):
            self.acquire("combat")

    def test_unreadable_lock_file_does_not_block_forever(self):
        self.path.parent.mkdir(parents=True)
        self.path.write_text("{", encoding="utf-8")
        self.acquire("home")
        self.assertEqual(editor_lock.read(self.path)["owner"], "home")

    def test_release_leaves_no_working_files(self):
        token = self.acquire("home")
        editor_lock.release(self.path, token)
        self.assertEqual(list(self.path.parent.iterdir()), [])

    def test_waiting_chats_take_the_lock_in_the_order_they_started_waiting(self):
        token = self.acquire("home")
        with self.assertRaises(editor_lock.Busy):
            self.acquire("combat")
        self.clock.current += 5
        with self.assertRaises(editor_lock.Busy):
            self.acquire("quest")
        editor_lock.release(self.path, token)
        with self.assertRaises(editor_lock.Busy) as raised:
            self.acquire("quest")
        self.assertIsNone(raised.exception.lock)
        self.assertEqual(raised.exception.ahead, ["combat"])
        self.acquire("combat")
        self.assertEqual(editor_lock.read(self.path)["owner"], "combat")

    def test_a_waiter_that_stops_checking_in_loses_its_place(self):
        token = self.acquire("home")
        with self.assertRaises(editor_lock.Busy):
            self.acquire("combat")
        editor_lock.release(self.path, token)
        self.clock.current += editor_lock.TICKET_GRACE_SECONDS + 1
        self.acquire("quest")
        self.assertEqual(editor_lock.read(self.path)["owner"], "quest")
        self.assertEqual(list(self.path.parent.glob("*.ticket")), [])

    def test_renewing_never_keeps_the_lock_past_the_hold_limit(self):
        token = self.acquire("home", lease=600)
        self.clock.current += 600 - 1
        editor_lock.renew(self.path, token, 600, now=self.clock.now)
        self.assertEqual(editor_lock.read(self.path)["expires"], 1000.0 + editor_lock.MAX_HOLD_SECONDS)
        self.clock.current = 1000.0 + editor_lock.MAX_HOLD_SECONDS
        self.acquire("combat")
        self.assertEqual(editor_lock.read(self.path)["owner"], "combat")

    def test_the_end_of_a_turn_returns_only_that_chats_idle_lock(self):
        token = editor_lock.acquire(self.path, "home", 0, 600, session="a", now=self.clock.now)
        self.assertFalse(editor_lock.release_session(self.path, "b"))
        self.assertEqual(editor_lock.read(self.path)["token"], token)
        self.assertTrue(editor_lock.release_session(self.path, "a"))
        self.assertIsNone(editor_lock.read(self.path))

    def test_the_end_of_a_turn_keeps_the_lock_while_its_script_runs(self):
        editor_lock.acquire(self.path, "tests", 0, 600, session="a", pid=os.getpid(), now=self.clock.now)
        self.assertFalse(editor_lock.release_session(self.path, "a"))
        finished = subprocess.run([sys.executable, "-c", "import os; print(os.getpid())"], capture_output=True, text=True)
        token = editor_lock.read(self.path)["token"]
        editor_lock.renew(self.path, token, 600, now=self.clock.now, pid=int(finished.stdout))
        self.assertTrue(editor_lock.release_session(self.path, "a"))

    def test_the_end_of_a_turn_gives_up_the_chats_place_in_line(self):
        self.acquire("home")
        with self.assertRaises(editor_lock.Busy):
            editor_lock.acquire(self.path, "combat", 0, 600, session="a", now=self.clock.now)
        editor_lock.release_session(self.path, "a")
        self.assertEqual(list(self.path.parent.glob("*.ticket")), [])


class CommandLineTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-editor-lock-cli-")
        self.addCleanup(temporary.cleanup)
        self.path = Path(temporary.name) / "editor-lock.json"

    def run_lock(self, *arguments):
        return subprocess.run(
            [sys.executable, "-B", str(SCRIPT), "--lock-file", str(self.path), *arguments],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_exit_codes_tell_busy_and_lost_tokens_apart(self):
        taken = self.run_lock("acquire", "--owner", "ホーム画面", "--wait", "0")
        self.assertEqual(taken.returncode, 0, taken.stdout + taken.stderr)
        token = taken.stdout.split("token=")[1].split()[0]

        busy = self.run_lock("acquire", "--owner", "戦闘画面", "--wait", "0")
        self.assertEqual(busy.returncode, editor_lock.EXIT_BUSY)
        self.assertIn("ホーム画面", busy.stdout)
        self.assertIn("ホーム画面", self.run_lock("status").stdout)

        self.assertEqual(self.run_lock("release", "wrong").returncode, editor_lock.EXIT_NOT_HELD)
        self.assertEqual(self.run_lock("renew", token).returncode, 0)
        self.assertEqual(self.run_lock("release", token).returncode, 0)
        self.assertEqual(self.run_lock("status").stdout.strip(), 'free; waiting: "戦闘画面"')

    def test_the_hook_returns_the_lock_of_the_session_it_is_given(self):
        taken = self.run_lock("acquire", "--owner", "ホーム画面", "--wait", "0", "--session", "chat-1")
        self.assertEqual(taken.returncode, 0, taken.stdout + taken.stderr)
        other = self.run_lock_with_input('{"session_id": "chat-2"}', "release-session")
        self.assertEqual(other.returncode, 0, other.stderr)
        self.assertIn("ホーム画面", self.run_lock("status").stdout)
        own = self.run_lock_with_input('{"session_id": "chat-1"}', "release-session")
        self.assertEqual(own.returncode, 0, own.stderr)
        self.assertEqual(self.run_lock("status").stdout.strip(), "free")

    def run_lock_with_input(self, stdin, *arguments):
        return subprocess.run(
            [sys.executable, "-B", str(SCRIPT), "--lock-file", str(self.path), *arguments],
            input=stdin,
            capture_output=True,
            text=True,
            check=False,
        )


if __name__ == "__main__":
    unittest.main()
