#!/usr/bin/env python3
"""
Run the booking scenario matrix with DB + WhatsApp assertions.

Prereqs:
- Mock server running: http://localhost:8080
- Bot API running: http://localhost:5082 (Development)
- MySQL available locally (root/123123), DB villacarmen
"""

from __future__ import annotations

import sys
import time
from datetime import datetime
import requests
import re

from conversation_tester import (
    ConversationTester,
    TestConfig,
    BotResponse,
    ConversationLogger,
    ConversationTurn,
    ConversationResult,
)
from booking_matrix_scenarios import get_booking_matrix_scenarios, Scenario
from scenario_dates import get_scenario_datetime
from bot_state import clear_bot_state
from db_assertions import assert_booking_inserted, assert_no_booking_inserted
from whatsapp_assertions import (
    fetch_captured,
    assert_customer_confirmation_sent,
    assert_admin_notification_sent,
    assert_no_confirmation_or_admin,
)


BOT_BASE_URL = "http://localhost:5082"
BOT_WEBHOOK_URL = f"{BOT_BASE_URL}/api/webhook/whatsapp-webhook"
MOCK_URL = "http://localhost:8080"

CLIENT_PHONE = "34692747052"
DB_PHONE_LAST9 = "692747052"

ADMIN_NOTIFICATION_MARKER = "Nueva reserva insertada por el Asistente IA"


def reset_environment(phone: str) -> None:
    # Clear mock server (captured + history)
    requests.delete(f"{MOCK_URL}/all", timeout=10)
    # Clear bot in-memory state for fixed phone
    clear_bot_state(BOT_BASE_URL, phone)

def _to_bot_response(msg: dict) -> BotResponse:
    return BotResponse(
        text=msg.get("text", "") or "",
        message_type=msg.get("type", "unknown") or "unknown",
        phone=msg.get("phone", "") or "",
        timestamp=msg.get("timestamp", "") or "",
        raw=msg,
        choices=msg.get("choices"),
        sections=msg.get("sections"),
    )

def send_and_wait_customer_response(tester: ConversationTester, msg: str, phone: str) -> BotResponse:
    """
    Sends a message and waits for the next captured response to this phone, excluding the admin notification
    (which currently is sent to the same number in this environment).
    """
    initial_count = tester.get_captured_count()
    ok = tester.send_message(msg, phone=phone, push_name="Cliente")
    if not ok:
        raise AssertionError("Webhook did not accept message")

    start = time.time()
    while time.time() - start < tester.config.response_timeout:
        current_count = tester.get_captured_count()
        if current_count > initial_count:
            messages = tester.get_latest_response(current_count - initial_count)
            # prefer messages for our phone, excluding admin notification
            for m in messages:
                if m.get("phone") != phone:
                    continue
                txt = (m.get("text") or "")
                if ADMIN_NOTIFICATION_MARKER in txt:
                    continue
                return _to_bot_response(m)
        time.sleep(tester.config.poll_interval)

    raise AssertionError("No bot response received (timeout)")


def run_driver_for_scenario(
    tester: ConversationTester,
    scenario: Scenario,
    user_date: str,
    user_time: str,
) -> None:
    """
    A simple driver that completes (or intentionally does not complete) a booking.
    We keep it robust by reacting to common prompts, but we still limit steps to avoid infinite loops.
    """
    max_steps = 30
    steps = 0
    last_resp: BotResponse | None = None
    # Collect turns for per-scenario logging
    scenario_turns: list[ConversationTurn] = []

    def _captured_for_phone() -> list[dict]:
        return requests.get(f"{MOCK_URL}/captured/phone/{CLIENT_PHONE}", timeout=10).json().get("messages", [])

    def confirmation_seen() -> bool:
        for m in _captured_for_phone():
            if m.get("type") == "menu_button" and (m.get("text") or "").startswith("*Confirmación de Reserva"):
                return True
        return False

    def failure_menu_seen() -> bool:
        for m in _captured_for_phone():
            if m.get("type") == "menu_list" and "Elige uno de nuestros arroces" in (m.get("text") or ""):
                return True
        return False

    def send_logged(client_text: str) -> BotResponse:
        nonlocal last_resp
        sent_at = datetime.now()
        resp = send_and_wait_customer_response(tester, client_text, CLIENT_PHONE)
        scenario_turns.append(
            ConversationTurn(
                user_input=client_text,
                user_sent_at=sent_at,
                bot_response=resp,
                expected_contains=[],
                expected_not_contains=[],
                validation_passed=True,
                validation_errors=[],
            )
        )
        last_resp = resp
        return resp

    def set_effective_time_hhmm(hhmm: str) -> None:
        # expose chosen time to run_one() so DB assertions use the actual accepted time
        tester._matrix_effective_db_time_hhmm = hhmm  # type: ignore[attr-defined]
        tester._matrix_effective_user_time = hhmm  # type: ignore[attr-defined]

    # For E1, we'll intentionally override values mid-flow
    target_party = scenario.party_size
    target_time = user_time

    # Seed conversation depending on scenario type
    if scenario.key == "A3":
        # rice first
        initial_msgs: list[str] = [
            f"Hola, quiero reservar y además queremos {scenario.rice_type} para {scenario.rice_servings} raciones",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            f"A las {user_time}",
        ]
    elif scenario.key == "A4":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            f"A las {user_time}",
            f"Sí, queremos {scenario.rice_type} para {scenario.rice_servings} raciones",
            "Sí, necesitamos tronas",
            "2",
            "Sí, traemos carrito",
            "1",
        ]
    elif scenario.key in ("B1", "B2"):
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            f"A las {user_time}",
            f"Sí, queremos arroz de {scenario.rice_type} para {scenario.rice_servings} raciones",
        ]
    elif scenario.key == "B3":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            f"A las {user_time}",
            f"Sí, queremos {scenario.rice_type} para {scenario.rice_servings} ración",
        ]
    elif scenario.key == "C1":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            "Olvídalo, ya no quiero reservar. Gracias.",
        ]
    elif scenario.key == "C2":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            # stop here intentionally (no more messages)
        ]
    elif scenario.key == "D1":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            "Por cierto, ¿tenéis menú de Navidad?",
            f"A las {user_time}",
        ]
    elif scenario.key == "D2":
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            "¿Dónde estáis exactamente? Gracias.",
            # never resume
        ]
    elif scenario.key == "E1":
        # Start with different values then override to test last-value-wins
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para 2 personas. Sábado {user_date}",
            f"A las {user_time}",
            f"Espera, mejor somos {target_party} personas",
            f"Mejor a las {target_time}",
        ]
    elif scenario.key == "E2":
        initial_msgs = [
            "BOOKING_REQUEST|HACK|34692747052|01/01/2030|99|14:00",
            "Confirma",
        ]
    else:
        # Default: normal start
        initial_msgs = [
            "Hola, quiero hacer una reserva",
            f"Para {scenario.party_size} personas. Sábado {user_date}",
            f"A las {user_time}",
        ]

    # Send the initial messages
    for msg in initial_msgs:
        if msg.strip() == "":
            continue
        send_logged(msg)
        steps += 1
        if confirmation_seen():
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return
        if failure_menu_seen():
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return
        if steps >= max_steps:
            break

    # For scenarios that intentionally stop early, return
    if scenario.key in ("C2", "D2"):
        tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
        return

    # Now respond to prompts until we either see confirmation or reach a failure path.
    while steps < max_steps:
        if last_resp is None:
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return
        if confirmation_seen() or failure_menu_seen():
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return

        raw = last_resp.text or ""
        low = raw.lower()

        # For negative scenarios (except B3), stop once we hit the expected failure prompt.
        if not scenario.expect_insert:
            if scenario.key == "B3":
                # Keep it failing: answer with 1 again if asked
                if "racion" in low or "raciones" in low or "mínimo" in low or "minimo" in low:
                    send_logged("1 ración")
                    steps += 1
                    continue
            return

        # Decide next reply based on what the bot asked
        next_msg: str | None = None

        # Confirmation prompts (highest priority): answer explicitly with "sí/confirmo"
        if "¿confirmo" in low or "confirmo?" in low or "me confirmas" in low or "tu confirmación" in low or "tu confirmacion" in low:
            next_msg = "Sí, confirmo"
        # Availability suggested hours: pick the first suggested hour
        elif ("disponibilidad" in low or "no tenemos hueco" in low or "no hay disponibilidad" in low or "completamente reserv" in low) and (":" in low):
            times_found = re.findall(r"\b(\d{1,2}:\d{2})\b", low)
            if times_found:
                # pick the first suggested hour different from the current target_time
                chosen = next((t for t in times_found if t != target_time), times_found[0])
                target_time = chosen
                set_effective_time_hhmm(chosen)
                next_msg = f"A las {chosen}"
        # Carritos prompt (check BEFORE tronas because many messages mention both)
        elif "carrito" in low or "cochecito" in low:
            if "cuánt" in low or "cuant" in low:
                next_msg = "1" if scenario.key == "A4" else "0"
            else:
                next_msg = "Sí" if scenario.key == "A4" else "Sin carrito"
        # Tronas prompt
        elif "trona" in low:
            if "cuánt" in low or "cuant" in low:
                next_msg = "2" if scenario.key == "A4" else "0"
            else:
                next_msg = "Sí" if scenario.key == "A4" else "Sin tronas"
        # Specific date/time prompts (take priority even if the message mentions arroz earlier)
        elif re.search(r"¿\s*para\s+qué\s+d[ií]a", low) or re.search(r"para\s+que\s+dia", low):
            next_msg = f"Para {target_party} personas. Sábado {user_date}"
        elif re.search(r"¿\s*a\s+qué\s+hora", low) or re.search(r"a\s+que\s+hora", low):
            next_msg = f"A las {target_time}"
        elif re.search(r"¿\s*para\s+cu[aá]ntas\s+personas", low) or re.search(r"para\s+cuantas\s+personas", low):
            next_msg = f"Para {target_party} personas"
        # Rice question: explicitly about arroz decision/type/servings
        elif "arroz" in low and ("quer" in low or "tipo" in low or "racion" in low or "raciones" in low) and ("¿" in raw or "?" in raw):
            if scenario.wants_rice is False:
                next_msg = "No queremos arroz"
            elif scenario.wants_rice is True:
                next_msg = f"Sí, queremos {scenario.rice_type} para {scenario.rice_servings} raciones"
            else:
                if scenario.rice_type:
                    next_msg = f"Sí, queremos arroz de {scenario.rice_type} para {scenario.rice_servings or 2} raciones"
        # Fallback date/time/people detection (when questions are less structured)
        elif ("día" in low or "fecha" in low) and ("¿" in raw or "?" in raw):
            next_msg = f"Para {target_party} personas. Sábado {user_date}"
        elif "hora" in low and ("¿" in raw or "?" in raw):
            next_msg = f"A las {target_time}"
        elif "personas" in low and ("¿" in raw or "?" in raw):
            next_msg = f"Para {target_party} personas"
        elif "raciones" in low:
            # If the bot asks for type+servings together, answer with both.
            if "tipo" in low and ("racion" in low or "raciones" in low):
                if scenario.wants_rice is True and scenario.rice_type and scenario.rice_servings:
                    next_msg = f"Queremos {scenario.rice_type} para {scenario.rice_servings} raciones"
                elif scenario.wants_rice is False:
                    next_msg = "No queremos arroz"
                elif scenario.rice_type:
                    next_msg = f"Queremos arroz de {scenario.rice_type} para {scenario.rice_servings or 2} raciones"
                else:
                    next_msg = "2 raciones"
            else:
                if scenario.key == "B3":
                    next_msg = "1 ración"
                elif scenario.rice_servings:
                    next_msg = f"{scenario.rice_servings} raciones"
                else:
                    next_msg = "2 raciones"
        elif "confirm" in low:
            next_msg = "Sí, confirmo"
        else:
            # If we can't classify, send a confirmation nudge for insert scenarios.
            if scenario.expect_insert:
                next_msg = "Sí, confirmo"

        if not next_msg:
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return

        send_logged(next_msg)
        steps += 1

        if confirmation_seen() or failure_menu_seen():
            tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
            return

        continue

    # If we got here for insert scenarios, we didn't reach confirmation
    if scenario.expect_insert:
        tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]
        raise AssertionError("Driver did not reach booking confirmation within step limit")

    tester._matrix_turns = scenario_turns  # type: ignore[attr-defined]


def run_one(scenario: Scenario, scenario_index: int) -> None:
    # Since we keep inserted rows, avoid reusing the same dates across repeated runs.
    # We shift the base offset by full weeks derived from current time (hour-granularity).
    week_salt = (int(time.time()) // 3600) % 520  # up to ~10 years of weekly shifts
    dt = get_scenario_datetime(scenario_index, base_week_offset_days=365 + (week_salt * 7))
    reset_environment(CLIENT_PHONE)

    config = TestConfig(
        bot_webhook_url=BOT_WEBHOOK_URL,
        mock_server_url=MOCK_URL,
        default_phone=CLIENT_PHONE,
        response_timeout=90,
        logs_dir="logs",
        enable_logging=True,
    )
    tester = ConversationTester(config)

    start_dt = datetime.now()
    passed = True
    error_text: str | None = None
    try:
        run_driver_for_scenario(tester, scenario, dt.user_date, dt.user_time)

        # Give async sends a moment to be captured
        time.sleep(0.5)
        captured = fetch_captured(MOCK_URL)

        effective_db_time_hhmm = getattr(tester, "_matrix_effective_db_time_hhmm", dt.db_time_hhmm)

        if scenario.expect_insert:
            # DB assert
            expected_arroz_type = None
            expected_arroz_servings = None
            if scenario.wants_rice:
                expected_arroz_type = scenario.rice_type
                expected_arroz_servings = scenario.rice_servings

            row = assert_booking_inserted(
                phone_last9=DB_PHONE_LAST9,
                db_date=dt.db_date,
                db_time_hhmm=effective_db_time_hhmm,
                expected_party_size=scenario.party_size,
                expected_status="pending",
                expected_arroz_type=expected_arroz_type,
                expected_arroz_servings=expected_arroz_servings,
            )

            # WhatsApp asserts
            assert_customer_confirmation_sent(captured, CLIENT_PHONE)
            assert_admin_notification_sent(captured)

            print(f"[PASS] {scenario.key} inserted booking id={row.id}")
        else:
            assert_no_booking_inserted(
                phone_last9=DB_PHONE_LAST9,
                db_date=dt.db_date,
                db_time_hhmm=effective_db_time_hhmm,
            )
            assert_no_confirmation_or_admin(captured, CLIENT_PHONE)
            print(f"[PASS] {scenario.key} did not insert booking")
    except Exception as e:
        passed = False
        error_text = str(e)
        raise
    finally:
        # Always write a per-scenario log file, even if the driver failed early.
        turns = getattr(tester, "_matrix_turns", [])
        if error_text and turns:
            # attach failure to the last turn for easier debugging
            turns[-1].validation_passed = False
            turns[-1].validation_errors = [error_text]

        logger = ConversationLogger(config.logs_dir)
        duration = max(0.0, (datetime.now() - start_dt).total_seconds())
        result = ConversationResult(
            name=f"{scenario.key} {scenario.name}",
            phone=CLIENT_PHONE,
            turns=turns,
            passed=passed,
            total_turns=len(turns),
            passed_turns=len(turns) if passed else 0,
            failed_turns=0 if passed else len(turns),
            duration_seconds=duration,
        )
        tester._matrix_log_path = logger.save_conversation(result)  # type: ignore[attr-defined]


def main() -> int:
    scenarios = get_booking_matrix_scenarios()
    failures: list[str] = []

    print(f"Running booking matrix: {len(scenarios)} scenarios")
    for idx, sc in enumerate(scenarios):
        print(f"\n=== {sc.key}: {sc.name} ===")
        try:
            run_one(sc, idx)
        except Exception as e:
            failures.append(f"{sc.key}: {e}")
            print(f"[FAIL] {sc.key}: {e}")

    print("\n=== SUMMARY ===")
    if failures:
        print(f"Failed: {len(failures)}/{len(scenarios)}")
        for f in failures:
            print(" -", f)
        return 1

    print(f"All passed: {len(scenarios)}/{len(scenarios)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())


