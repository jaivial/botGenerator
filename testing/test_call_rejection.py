#!/usr/bin/env python3
"""
Black-box test: inbound WhatsApp call event should be rejected via UAZAPI and auto-replied with text + vCard.

Prereqs:
  - Mock UAZAPI server running on http://localhost:8080
  - Bot running with WHATSAPP_API_URL=http://localhost:8080
"""

import os
import sys
import requests


BOT_WEBHOOK_URL = os.environ.get("BOT_WEBHOOK_URL", "http://localhost:5082/api/webhook/whatsapp-webhook")
MOCK_BASE_URL = os.environ.get("MOCK_UAZAPI_URL", "http://localhost:8080")


def main() -> int:
    # Clear captured
    requests.delete(f"{MOCK_BASE_URL}/captured", timeout=5)

    payload = {
        "EventType": "call",
        "call": {
            "chatid": "34600000000@s.whatsapp.net",
            "id": "call-123"
        }
    }

    r = requests.post(BOT_WEBHOOK_URL, json=payload, timeout=10)
    if r.status_code != 200:
        print(f"FAIL: webhook returned {r.status_code}: {r.text}")
        return 2

    captured = requests.get(f"{MOCK_BASE_URL}/captured", timeout=5).json()
    msgs = captured.get("messages", [])
    types = [m.get("type") for m in msgs]

    if "call_reject" not in types:
        print(f"FAIL: expected call_reject in captured types, got: {types}")
        return 3
    if "text" not in types:
        print(f"FAIL: expected text auto-reply in captured types, got: {types}")
        return 4
    if "contact" not in types:
        print(f"FAIL: expected contact auto-reply in captured types, got: {types}")
        return 5

    print("PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

