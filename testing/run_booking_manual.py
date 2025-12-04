#!/usr/bin/env python3
"""
Manual Full Booking Test
Run this after manually starting:
1. Mock server: python mock_uazapi_server.py
2. Bot: WHATSAPP_API_URL=http://localhost:8080 dotnet run --project src/BotGenerator.Api
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from conversation_tester import ConversationTester, TestConfig


def run_full_booking_test():
    """Run a complete booking test with valid rice type"""
    config = TestConfig(
        bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
        mock_server_url="http://localhost:8080",
        response_timeout=30,
        logs_dir="logs"
    )

    print("Connecting to servers...")
    tester = ConversationTester(config)
    print("Connected!")

    # Full booking conversation with valid rice (Arroz de chorizo)
    test_scenario = {
        "name": "Full Booking with Valid Rice (Arroz de chorizo)",
        "phone": "34612345681",
        "turns": [
            {
                "input": "Hola, quiero hacer una reserva para el sabado a las 14:00 para 4 personas",
                "expected_contains": [],
                "expected_not_contains": ["error"]
            },
            {
                "input": "Si, queremos arroz de chorizo para 4 raciones",
                "expected_contains": ["chorizo"],
                "expected_not_contains": ["no tenemos", "no existe"]
            },
            {
                "input": "Mi nombre es Juan Garcia",
                "expected_contains": [],
                "expected_not_contains": ["error"]
            },
            {
                "input": "Si, confirmo la reserva",
                "expected_contains": [],
                "expected_not_contains": ["error"]
            }
        ]
    }

    print("\n" + "=" * 60)
    print("RUNNING FULL BOOKING TEST")
    print("=" * 60)

    result = tester.run_conversation(
        name=test_scenario["name"],
        turns=test_scenario["turns"],
        phone=test_scenario["phone"]
    )

    # Print full conversation
    print("\n" + "=" * 60)
    print("FULL CONVERSATION")
    print("=" * 60)
    for i, turn in enumerate(result.turns, 1):
        print(f"\n[Turn {i}]")
        print(f"  User: {turn.user_input}")
        if turn.bot_response:
            print(f"  Bot: {turn.bot_response.text}")
        else:
            print("  Bot: [NO RESPONSE]")

    print("\n" + "=" * 60)
    print(f"Result: {'PASSED' if result.passed else 'FAILED'}")
    print(f"Duration: {result.duration_seconds:.1f}s")
    print("=" * 60)

    return result


if __name__ == "__main__":
    print("=" * 60)
    print("MANUAL FULL BOOKING TEST")
    print("=" * 60)
    print()
    print("This test expects servers to be running already:")
    print("1. Mock server: python mock_uazapi_server.py")
    print("2. Bot: WHATSAPP_API_URL=http://localhost:8080 dotnet run --project src/BotGenerator.Api")
    print()

    try:
        result = run_full_booking_test()
        exit(0 if result.passed else 1)
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
        exit(1)
