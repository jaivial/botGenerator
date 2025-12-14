from __future__ import annotations

import requests


def fetch_captured(mock_url: str) -> list[dict]:
    resp = requests.get(f"{mock_url}/captured", timeout=10)
    resp.raise_for_status()
    return resp.json().get("messages", [])


def filter_by_phone(messages: list[dict], phone: str) -> list[dict]:
    return [m for m in messages if m.get("phone") == phone]


def assert_customer_confirmation_sent(messages: list[dict], phone: str) -> None:
    phone_msgs = filter_by_phone(messages, phone)
    # Confirmation is sent as a menu_button (link buttons) and starts with a known header.
    for m in phone_msgs:
        if m.get("type") == "menu_button" and (m.get("text") or "").startswith("*Confirmación de Reserva"):
            return
    raise AssertionError(f"Expected customer confirmation (menu_button) to be sent to {phone}, but did not find it.")


def assert_admin_notification_sent(messages: list[dict]) -> None:
    # Admin notification is plain text containing this header.
    for m in messages:
        txt = m.get("text") or ""
        if "Nueva reserva insertada por el Asistente IA" in txt:
            return
    raise AssertionError("Expected admin notification text to be sent, but did not find it in captured messages.")


def assert_no_confirmation_or_admin(messages: list[dict], phone: str) -> None:
    phone_msgs = filter_by_phone(messages, phone)
    for m in phone_msgs:
        txt = m.get("text") or ""
        if txt.startswith("*Confirmación de Reserva"):
            raise AssertionError(f"Did not expect customer confirmation to be sent to {phone}, but it was captured.")
        if "Nueva reserva insertada por el Asistente IA" in txt:
            raise AssertionError("Did not expect admin notification, but it was captured.")


