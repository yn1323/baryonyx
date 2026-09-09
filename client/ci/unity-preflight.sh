#!/usr/bin/env bash
set -euo pipefail

missing=0
for name in UNITY_LICENSE UNITY_EMAIL UNITY_PASSWORD; do
  if [ -z "${!name:-}" ]; then
    echo "::error::Missing $name. See doc/rules/client-code-quality.md. Fork PRs cannot access repository secrets."
    missing=1
  fi
done
if [ "$missing" -ne 0 ]; then
  exit 1
fi

version=$(awk '/^m_EditorVersion: / {print $2}' client/ProjectSettings/ProjectVersion.txt | tr -d '\r')
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+$ ]]
echo "UNITY_VERSION=$version" >> "$GITHUB_ENV"
