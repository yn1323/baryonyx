"""Create or replace an environment's APK in the configured Google Drive folder."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen


API = "https://www.googleapis.com/drive/v3/files"
UPLOAD_API = "https://www.googleapis.com/upload/drive/v3/files"
ENVIRONMENTS = ("dev", "prod", "preview")
APK_MIME = "application/vnd.android.package-archive"
FOLDER_MIME = "application/vnd.google-apps.folder"
SAFE_ERROR_CODES = frozenset({
    "invalid_request", "invalid_client", "invalid_grant", "unauthorized_client",
    "unsupported_grant_type", "invalid_scope", "access_denied",
    "badRequest", "notFound", "authError", "insufficientPermissions",
    "insufficientFilePermissions", "storageQuotaExceeded", "rateLimitExceeded",
    "userRateLimitExceeded", "dailyLimitExceeded", "domainPolicy",
})


def safe_error_code(error):
    # Free-form messages can contain tokens, IDs or personal data. Never return them.
    try:
        payload = json.loads(error.read(4096))
    except (ValueError, OSError):
        return None
    detail = payload.get("error") if isinstance(payload, dict) else None
    if isinstance(detail, str):
        return detail if detail in SAFE_ERROR_CODES else None
    reasons = detail.get("errors") if isinstance(detail, dict) else None
    if isinstance(reasons, list):
        for item in reasons:
            code = item.get("reason") if isinstance(item, dict) else None
            if isinstance(code, str) and code in SAFE_ERROR_CODES:
                return code
    return None


def request(method, url, *, operation, token=None, data=None, headers=None):
    headers = dict(headers or {})
    if token:
        headers["Authorization"] = f"Bearer {token}"
    try:
        with urlopen(Request(url, data=data, headers=headers, method=method), timeout=180) as response:
            body = response.read()
            return (json.loads(body) if body else {}), response.headers
    except HTTPError as error:
        code = safe_error_code(error)
        detail = f", {code}" if code else ""
        raise RuntimeError(f"{operation} failed (HTTP {error.code}{detail}).") from None
    except (URLError, TimeoutError):
        raise RuntimeError(f"{operation} connection failed; rerun the upload job.") from None


def require_id(value):
    if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9_-]+", value):
        raise ValueError("Invalid Google Drive file/folder ID.")
    return value


def refresh_access_token(env):
    names = ("GOOGLE_DRIVE_CLIENT_ID", "GOOGLE_DRIVE_CLIENT_SECRET", "GOOGLE_DRIVE_REFRESH_TOKEN")
    missing = [name for name in names if not env.get(name)]
    if missing:
        raise ValueError("Missing GitHub Actions secrets: " + ", ".join(missing))
    result, _ = request(
        "POST", "https://oauth2.googleapis.com/token",
        operation="OAuth token refresh",
        data=urlencode({
            "client_id": env[names[0]],
            "client_secret": env[names[1]],
            "refresh_token": env[names[2]],
            "grant_type": "refresh_token",
        }).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    token = result.get("access_token")
    if not token:
        raise RuntimeError("Google OAuth did not return an access token.")
    return token


def find_existing_file(folder_id, token, apk_name):
    params = {
        "q": f"'{folder_id}' in parents and name = '{apk_name}' and trashed = false",
        "spaces": "drive",
        "fields": "files(id,mimeType),nextPageToken",
        "pageSize": 100,
    }
    files = []
    while True:
        page, _ = request("GET", API + "?" + urlencode(params), operation="Drive file lookup", token=token)
        files.extend(page.get("files", []))
        if len(files) > 1:
            raise ValueError(f"Multiple {apk_name} files exist in the destination; keep only one before uploading.")
        if not page.get("nextPageToken"):
            break
        params["pageToken"] = page["nextPageToken"]
    if not files:
        return None
    if files[0].get("mimeType", "").startswith("application/vnd.google-apps."):
        raise ValueError("The destination name belongs to a Google document, folder or shortcut.")
    return require_id(files[0]["id"])


def upload(apk, folder_id, env, environment):
    if environment not in ENVIRONMENTS:
        raise ValueError("APK environment must be dev, prod or preview.")
    apk_name = f"baryonyx-{environment}.apk"
    folder_id = require_id(folder_id)
    size = apk.stat().st_size
    if apk.suffix.lower() != ".apk" or size == 0:
        raise ValueError("A non-empty .apk file is required.")
    token = refresh_access_token(env)
    folder, _ = request(
        "GET", API + "/" + folder_id + "?" + urlencode({
            "fields": "mimeType,trashed,capabilities(canAddChildren)",
        }), operation="Drive folder lookup", token=token,
    )
    if (folder.get("mimeType") != FOLDER_MIME or folder.get("trashed")
            or not folder.get("capabilities", {}).get("canAddChildren")):
        raise ValueError("The destination must be a writable, non-trashed Google Drive folder.")
    existing_id = find_existing_file(folder_id, token, apk_name)
    metadata = {"name": apk_name, "mimeType": APK_MIME}
    source_sha = env.get("SOURCE_HEAD_SHA") or env.get("GITHUB_SHA")
    source_run = env.get("SOURCE_RUN_ID") or env.get("GITHUB_RUN_ID")
    if source_sha:
        metadata["description"] = (
            f"Distribution: {environment}\nCommit: {source_sha}\n"
            f"CI: https://github.com/{env['GITHUB_REPOSITORY']}/actions/runs/{source_run}"
        )
    url = UPLOAD_API
    if existing_id:
        url += "/" + existing_id
    else:
        metadata["parents"] = [folder_id]
    url += "?" + urlencode({"uploadType": "resumable", "fields": "id,size,md5Checksum"})
    _, headers = request(
        "PATCH" if existing_id else "POST", url, token=token,
        operation="Drive upload session",
        data=json.dumps(metadata).encode(),
        headers={
            "Content-Type": "application/json; charset=UTF-8",
            "X-Upload-Content-Type": APK_MIME,
            "X-Upload-Content-Length": str(size),
        },
    )
    session_url = headers.get("Location", "")
    parsed = urlsplit(session_url)
    if (parsed.scheme != "https" or parsed.netloc != "www.googleapis.com"
            or parsed.path != "/upload/drive/v3/files" and not parsed.path.startswith("/upload/drive/v3/files/")):
        raise RuntimeError("Google Drive returned an unexpected upload session URL.")
    # Drive supports sending the entire file in one PUT within a resumable session.
    content = apk.read_bytes()
    result, _ = request(
        "PUT", session_url, token=token, data=content,
        operation="Drive APK upload",
        headers={"Content-Type": APK_MIME, "Content-Length": str(len(content))},
    )
    file_id = require_id(result.get("id"))
    if existing_id and file_id != existing_id:
        raise RuntimeError("The upload returned a different file ID.")
    if str(result.get("size")) != str(len(content)) or result.get("md5Checksum") != hashlib.md5(content).hexdigest():
        raise RuntimeError("Uploaded APK size or checksum does not match the local file.")
    return f"https://drive.google.com/file/d/{file_id}/view"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--folder-id", required=True)
    parser.add_argument("--environment", choices=ENVIRONMENTS, required=True)
    args = parser.parse_args()
    try:
        url = upload(args.apk, args.folder_id, os.environ, args.environment)
    except (ValueError, RuntimeError, OSError) as error:
        print(f"Drive upload failed: {error}", file=sys.stderr)
        return 1
    apk_name = f"baryonyx-{args.environment}.apk"
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            output.write(f"drive-url={url}\n")
            output.write(f"apk-name={apk_name}\n")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
            summary.write(f"[{apk_name}をGoogle Driveから取得]({url})\n\n同名ファイルは同じ環境の次の配布時に更新されます。\n")
    print(f"Uploaded {apk_name}: {url}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
