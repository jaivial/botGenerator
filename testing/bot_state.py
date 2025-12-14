from __future__ import annotations

import requests


def clear_bot_state(bot_base_url: str, phone: str) -> None:
    """
    Clears in-memory bot state for a given phone via the Development-only API endpoint.

    bot_base_url example: http://localhost:5082
    """
    url = f"{bot_base_url}/api/webhook/test/clear-state"
    resp = requests.post(url, params={"phone": phone}, timeout=10)
    resp.raise_for_status()


