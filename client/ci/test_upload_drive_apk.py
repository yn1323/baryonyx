"""Exercise Drive create/update and failure paths without external writes."""

import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlsplit


spec = importlib.util.spec_from_file_location("upload_drive_apk", Path(__file__).with_name("upload-drive-apk.py"))
uploader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(uploader)

ENV = {
    "GOOGLE_DRIVE_CLIENT_ID": "test-client",
    "GOOGLE_DRIVE_CLIENT_SECRET": "test-secret",
    "GOOGLE_DRIVE_REFRESH_TOKEN": "test-refresh",
}
CONTENT = b"APK test content"
SESSION = "https://www.googleapis.com/upload/drive/v3/files?upload_id=test"


class Response(io.BytesIO):
    def __init__(self, data, headers=None):
        super().__init__(json.dumps(data).encode())
        self.headers = headers or {}


class UploadTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.apk = Path(temporary.name) / "build.apk"
        self.apk.write_bytes(CONTENT)

    def responses(self, files=None, folder=None, result=None, session=SESSION):
        return [
            Response({"access_token": "test-access"}),
            Response(folder or {"mimeType": uploader.FOLDER_MIME, "capabilities": {"canAddChildren": True}}),
            Response({"files": files or []}),
            Response({}, {"Location": session}),
            Response(result or {"id": "file-123", "size": str(len(CONTENT)), "md5Checksum": hashlib.md5(CONTENT).hexdigest()}),
        ]

    def test_create_in_requested_folder_and_send_raw_apk(self):
        with patch.object(uploader, "urlopen", side_effect=self.responses()) as http:
            url = uploader.upload(self.apk, "folder-123", ENV, "dev")
        calls = [call.args[0] for call in http.call_args_list]
        self.assertEqual(url, "https://drive.google.com/file/d/file-123/view")
        auth = parse_qs(calls[0].data.decode())
        self.assertEqual(auth["grant_type"], ["refresh_token"])
        self.assertEqual(auth["refresh_token"], ["test-refresh"])
        query = parse_qs(urlsplit(calls[2].full_url).query)["q"][0]
        self.assertIn("'folder-123' in parents", query)
        self.assertIn("name = 'baryonyx-dev.apk'", query)
        self.assertIn("trashed = false", query)
        self.assertEqual(calls[3].method, "POST")
        metadata = json.loads(calls[3].data)
        self.assertEqual(metadata["parents"], ["folder-123"])
        self.assertEqual(metadata["name"], "baryonyx-dev.apk")
        self.assertEqual(calls[4].method, "PUT")
        self.assertEqual(calls[4].data, CONTENT)
        self.assertEqual(calls[4].get_header("Content-type"), uploader.APK_MIME)
        self.assertEqual(calls[4].get_header("Authorization"), "Bearer test-access")

    def test_update_preserves_file_id_and_parent(self):
        responses = self.responses(files=[{"id": "file-123", "mimeType": uploader.APK_MIME}])
        with patch.object(uploader, "urlopen", side_effect=responses) as http:
            uploader.upload(self.apk, "folder-123", ENV, "dev")
        start = http.call_args_list[3].args[0]
        self.assertEqual(start.method, "PATCH")
        self.assertEqual(urlsplit(start.full_url).path, "/upload/drive/v3/files/file-123")
        self.assertNotIn("parents", json.loads(start.data))

    def test_description_references_source_build_instead_of_distribution_run(self):
        env = {**ENV, "SOURCE_HEAD_SHA": "source-sha", "SOURCE_RUN_ID": "100",
               "GITHUB_SHA": "trusted-sha", "GITHUB_RUN_ID": "200", "GITHUB_REPOSITORY": "owner/repo"}
        with patch.object(uploader, "urlopen", side_effect=self.responses()) as http:
            uploader.upload(self.apk, "folder-123", env, "dev")
        description = json.loads(http.call_args_list[3].args[0].data)["description"]
        self.assertIn("Commit: source-sha", description)
        self.assertIn("actions/runs/100", description)
        self.assertNotIn("trusted-sha", description)

    def test_each_environment_searches_and_updates_only_its_own_name(self):
        for environment in ("dev", "prod", "preview"):
            with self.subTest(environment=environment), patch.object(uploader, "urlopen", side_effect=self.responses(files=[{"id": "file-123", "mimeType": uploader.APK_MIME}])) as http:
                uploader.upload(self.apk, "folder-123", ENV, environment)
            query = parse_qs(urlsplit(http.call_args_list[2].args[0].full_url).query)["q"][0]
            self.assertEqual(query, f"'folder-123' in parents and name = 'baryonyx-{environment}.apk' and trashed = false")
            start = http.call_args_list[3].args[0]
            self.assertEqual(start.method, "PATCH")
            self.assertEqual(json.loads(start.data)["name"], f"baryonyx-{environment}.apk")

    def test_unknown_environment_fails_before_network(self):
        with patch.object(uploader, "urlopen") as http:
            with self.assertRaisesRegex(ValueError, "environment"):
                uploader.upload(self.apk, "folder-123", ENV, "unexpected")
        http.assert_not_called()

    def test_paginated_duplicate_names_fail_without_writes(self):
        responses = self.responses()[:2] + [
            Response({"files": [{"id": "first"}], "nextPageToken": "next"}),
            Response({"files": [{"id": "second"}]}),
        ]
        with patch.object(uploader, "urlopen", side_effect=responses) as http:
            with self.assertRaisesRegex(ValueError, "Multiple"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_count, 4)
        self.assertEqual(parse_qs(urlsplit(http.call_args.args[0].full_url).query)["pageToken"], ["next"])

    def test_empty_page_with_next_token_does_not_create_duplicate(self):
        responses = self.responses(files=[{"id": "file-123", "mimeType": uploader.APK_MIME}])
        responses.insert(2, Response({"files": [], "nextPageToken": "next"}))
        with patch.object(uploader, "urlopen", side_effect=responses) as http:
            uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_args_list[4].args[0].method, "PATCH")

    def test_rejects_unwritable_trashed_or_nonfolder_destination(self):
        folders = [
            {"mimeType": uploader.APK_MIME, "capabilities": {"canAddChildren": True}},
            {"mimeType": uploader.FOLDER_MIME, "capabilities": {"canAddChildren": False}},
            {"mimeType": uploader.FOLDER_MIME, "trashed": True, "capabilities": {"canAddChildren": True}},
        ]
        for folder in folders:
            with self.subTest(folder=folder), patch.object(uploader, "urlopen", side_effect=self.responses(folder=folder)) as http:
                with self.assertRaisesRegex(ValueError, "writable"):
                    uploader.upload(self.apk, "folder-123", ENV, "dev")
                self.assertEqual(http.call_count, 2)

    def test_same_name_folder_is_not_overwritten(self):
        with patch.object(uploader, "urlopen", side_effect=self.responses(files=[{"id": "folder", "mimeType": uploader.FOLDER_MIME}])) as http:
            with self.assertRaisesRegex(ValueError, "folder or shortcut"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_count, 3)

    def test_missing_credentials_fail_before_network(self):
        with patch.object(uploader, "urlopen") as http:
            with self.assertRaisesRegex(ValueError, "GOOGLE_DRIVE_CLIENT_ID"):
                uploader.upload(self.apk, "folder-123", {}, "dev")
        http.assert_not_called()

    def test_empty_apk_fails_before_network(self):
        self.apk.write_bytes(b"")
        with patch.object(uploader, "urlopen") as http:
            with self.assertRaisesRegex(ValueError, "non-empty"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        http.assert_not_called()

    def test_bad_checksum_is_not_reported_as_success(self):
        with patch.object(uploader, "urlopen", side_effect=self.responses(result={"id": "file-123", "size": str(len(CONTENT)), "md5Checksum": "wrong"})):
            with self.assertRaisesRegex(RuntimeError, "checksum"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")

    def test_untrusted_session_url_never_receives_token_or_apk(self):
        with patch.object(uploader, "urlopen", side_effect=self.responses(session="https://example.com/upload")) as http:
            with self.assertRaisesRegex(RuntimeError, "session URL"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_count, 4)

    def test_http_error_does_not_expose_response_body(self):
        error = HTTPError("https://oauth2.googleapis.com/token", 400, "secret-response", {}, io.BytesIO(b"test-refresh"))
        with patch.object(uploader, "urlopen", side_effect=error):
            with self.assertRaisesRegex(RuntimeError, r"^OAuth token refresh failed \(HTTP 400\)\.$"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")

    def test_oauth_error_reports_only_known_code_and_operation(self):
        payload = {"error": "invalid_grant", "error_description": "test-refresh test-secret user@example.com"}
        error = HTTPError("https://oauth2.googleapis.com/token", 400, "private-reason", {}, io.BytesIO(json.dumps(payload).encode()))
        with patch.object(uploader, "urlopen", side_effect=error) as http:
            with self.assertRaisesRegex(RuntimeError, r"^OAuth token refresh failed \(HTTP 400, invalid_grant\)\.$"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_count, 1)

    def test_folder_error_is_distinct_from_authentication_error(self):
        payload = {"error": {"message": "private-folder", "errors": [{"reason": "notFound", "message": "private-folder"}]}}
        error = HTTPError("https://www.googleapis.com/drive/v3/files/private-folder", 404, "private-reason", {}, io.BytesIO(json.dumps(payload).encode()))
        with patch.object(uploader, "urlopen", side_effect=[Response({"access_token": "test-access"}), error]) as http:
            with self.assertRaisesRegex(RuntimeError, r"^Drive folder lookup failed \(HTTP 404, notFound\)\.$"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertEqual(http.call_count, 2)

    def test_unknown_or_malformed_error_payload_cannot_leak_details(self):
        for payload in [
            {"error": "test-refresh"}, {"error": {"errors": [{"reason": "test-secret"}]}},
            {"error": {"errors": "test-access"}}, {"error": ["test-refresh"]},
            ["test-secret"], {"error": {"errors": [{"reason": ["test-secret"]}]}},
        ]:
            error = HTTPError("https://oauth2.googleapis.com/token", 400, "private-reason", {}, io.BytesIO(json.dumps(payload).encode()))
            with self.subTest(payload=payload), patch.object(uploader, "urlopen", side_effect=error):
                with self.assertRaisesRegex(RuntimeError, r"^OAuth token refresh failed \(HTTP 400\)\.$"):
                    uploader.upload(self.apk, "folder-123", ENV, "dev")

    def test_file_search_session_and_upload_failures_identify_the_operation(self):
        for index, operation in [(2, "Drive file lookup"), (3, "Drive upload session"), (4, "Drive APK upload")]:
            responses = self.responses()[:index]
            payload = {"error": {"errors": [{"reason": "badRequest", "message": "test-refresh"}]}}
            responses.append(HTTPError(SESSION, 400, "private-reason", {}, io.BytesIO(json.dumps(payload).encode())))
            with self.subTest(operation=operation), patch.object(uploader, "urlopen", side_effect=responses):
                with self.assertRaises(RuntimeError) as caught:
                    uploader.upload(self.apk, "folder-123", ENV, "dev")
            self.assertEqual(str(caught.exception), f"{operation} failed (HTTP 400, badRequest).")

    def test_upload_failure_does_not_delete_existing_file(self):
        responses = self.responses(files=[{"id": "file-123", "mimeType": uploader.APK_MIME}])
        responses[-1] = HTTPError(SESSION, 503, "Unavailable", {}, None)
        with patch.object(uploader, "urlopen", side_effect=responses) as http:
            with self.assertRaisesRegex(RuntimeError, "HTTP 503"):
                uploader.upload(self.apk, "folder-123", ENV, "dev")
        self.assertNotIn("DELETE", [call.args[0].method for call in http.call_args_list])


if __name__ == "__main__":
    unittest.main()
