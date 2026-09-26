#!/usr/bin/env python3
"""Prepare the Desktop client from an Actions secret without logging its value."""
import argparse
import json
import os
from pathlib import Path
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--required", action="store_true")
    args = parser.parse_args()
    value = os.environ.get("PULSEDECK_GMAIL_OAUTH_CLIENT_JSON", "")
    if not value.strip():
        if args.required:
            sys.exit("Missing GitHub Actions secret: PULSEDECK_GMAIL_OAUTH_CLIENT_JSON.")
        print("No bundled Gmail client configured; developer builds support manual import.")
        return

    try:
        if len(value) > 16384:
            raise ValueError()
        root = json.loads(value)
        client = root["installed"]
        client_id, client_secret = client["client_id"], client["client_secret"]
        if (not isinstance(client_id, str) or len(client_id) > 256
                or not client_id.endswith(".apps.googleusercontent.com")
                or not isinstance(client_secret, str) or not client_secret.strip()
                or len(client_secret) > 256):
            raise ValueError()
        # Reject user credentials rather than allowing them into the build input.
        def has_token(item):
            if isinstance(item, dict):
                return any(key in {"refresh_token", "access_token", "RefreshToken"}
                           or has_token(child) for key, child in item.items())
            if isinstance(item, list):
                return any(has_token(child) for child in item)
            return False
        if has_token(root):
            raise ValueError()
    except (ValueError, KeyError, TypeError):
        sys.exit("Invalid OAuth client: provide a Google Desktop client JSON without user tokens.")

    destination = Path(__file__).resolve().parents[1] / "apps/agent/gmail.oauth-client.json"
    with os.fdopen(os.open(destination, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600), "w") as output:
        os.chmod(destination, 0o600)
        json.dump({"installed": {"client_id": client_id, "client_secret": client_secret}}, output)
    print("Bundled Gmail Desktop client prepared; no user tokens included.")


if __name__ == "__main__":
    main()
