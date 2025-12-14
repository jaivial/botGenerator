#!/usr/bin/env python3
"""
Tough Client Tests for WhatsApp Bot

These tests simulate difficult/challenging clients with edge cases:
1. Non-existing rice types (persistent client)
2. More than 10 people (large group)
3. Two rice types request (only 1 allowed)
4. Simple successful booking (baseline)
5. Combination chaos (multiple issues at once)

Each test includes explicit time selection to handle availability conflicts.
"""

import sys
from datetime import datetime, timedelta
from conversation_tester import ConversationTester, TestConfig, run_test_suite


def get_next_saturday() -> str:
    """Get the next Saturday date in dd/mm/yyyy format."""
    today = datetime.now()
    days_until_saturday = (5 - today.weekday()) % 7
    if days_until_saturday == 0:
        days_until_saturday = 7
    next_sat = today + timedelta(days=days_until_saturday)
    return next_sat.strftime("%d/%m/%Y")


def get_tomorrow() -> str:
    """Get tomorrow's date."""
    tomorrow = datetime.now() + timedelta(days=1)
    return tomorrow.strftime("%d/%m/%Y")


# Get dynamic dates
SATURDAY = get_next_saturday()
TOMORROW = get_tomorrow()


# Test 1: Non-Existing Rice Types (Persistent Client)
TEST_1_INVALID_RICES = {
    "name": "Test 1: Non-Existing Rice Types (Persistent Client)",
    "phone": "34611111111",
    "turns": [
        {
            "input": "Hola, quiero reservar para el domingo a las 13:30 para 4 personas",
            "expected_contains": [],
            "expected_not_contains": ["error"]
        },
        {
            "input": "Quiero arroz de pollo",
            "expected_contains": ["no tenemos"],
            "expected_not_contains": []
        },
        {
            "input": "Entonces arroz tres delicias",
            "expected_contains": ["no tenemos"],
            "expected_not_contains": []
        },
        {
            "input": "Y paella mixta?",
            "expected_contains": ["no tenemos"],
            "expected_not_contains": []
        },
        {
            "input": "Vale, paella valenciana, 3 raciones",
            "expected_contains": [],
            "expected_not_contains": ["no tenemos"]
        },
        {
            "input": "0 tronas, 0 carritos",
            "expected_contains": [],
            "expected_not_contains": ["error"]
        },
        {
            "input": "Sí, confirmo la reserva",
            "expected_contains": ["Confirmación", "confirmada"],
            "expected_not_contains": ["error"]
        }
    ]
}


# Test 2: Insisting on More Than 10 People
# If Saturday is full, accept Thursday alternative
TEST_2_LARGE_GROUP = {
    "name": "Test 2: Insisting on More Than 10 People",
    "phone": "34622222222",
    "turns": [
        {
            "input": "Hola, quiero reservar para 15 personas",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Es que somos muchos, no se puede por el bot?",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Vale, entonces solo 8 personas para el domingo a las 14:30",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "No queremos arroz, 0 tronas, 0 carritos",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Sí, confirmo la reserva",
            "expected_contains": ["Confirmación", "confirmada"],
            "expected_not_contains": ["error"]
        }
    ]
}


# Test 3: Two Rice Types Request (Only 1 Allowed)
# Use Sunday since Saturday gets filled by other tests
TEST_3_TWO_RICE_TYPES = {
    "name": "Test 3: Two Rice Types Request (Only 1 Allowed)",
    "phone": "34633333333",
    "turns": [
        {
            "input": "Buenas, reserva para el domingo a las 14:00 para 6 personas",
            "expected_contains": [],
            "expected_not_contains": ["error"]
        },
        {
            "input": "Queremos paella valenciana y arroz negro",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Pero necesitamos los dos tipos de arroz porque somos muchos",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Vale, solo arroz negro, 4 raciones",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "0 tronas, 0 carritos",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Sí, confirmo la reserva",
            "expected_contains": ["Confirmación", "confirmada"],
            "expected_not_contains": ["error"]
        }
    ]
}


# Test 4: Simple Successful Booking (Baseline)
TEST_4_SIMPLE_SUCCESS = {
    "name": "Test 4: Simple Successful Booking",
    "phone": "34644444444",
    "turns": [
        {
            "input": "Hola, quiero hacer una reserva para el sábado a las 15:00 para 5 personas",
            "expected_contains": [],
            "expected_not_contains": ["error"]
        },
        {
            "input": "Sí, queremos arroz de señoret, 3 raciones",
            "expected_contains": [],
            "expected_not_contains": ["no tenemos"]
        },
        {
            "input": "Necesitamos 1 trona y 0 carritos",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Sí, confirmo la reserva",
            "expected_contains": ["Confirmación", "confirmada"],
            "expected_not_contains": ["error"]
        }
    ]
}


# Test 5: Combination Chaos (Multiple Issues)
# Uses Sunday 15:00 to avoid conflicts with other tests
TEST_5_COMBINATION_CHAOS = {
    "name": "Test 5: Combination Chaos (Multiple Issues)",
    "phone": "34655555555",
    "turns": [
        {
            "input": "Reservar para hoy a las 19:30 para 12 personas",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Vale, pues 8 personas para el domingo a las 15:00",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Quiero arroz de bogavante",
            "expected_contains": ["no tenemos"],
            "expected_not_contains": []
        },
        {
            "input": "Pues arroz negro, 2 raciones",
            "expected_contains": [],
            "expected_not_contains": ["no tenemos"]
        },
        {
            "input": "2 tronas, 1 carrito",
            "expected_contains": [],
            "expected_not_contains": []
        },
        {
            "input": "Sí, confirmo la reserva",
            "expected_contains": ["Confirmación", "confirmada"],
            "expected_not_contains": ["error"]
        }
    ]
}


def run_single_test(test_scenario: dict, config: TestConfig) -> dict:
    """Run a single test scenario and return the result."""
    tester = ConversationTester(config)
    result = tester.run_conversation(
        name=test_scenario["name"],
        turns=test_scenario["turns"],
        phone=test_scenario.get("phone"),
        clear_before=True
    )
    return {
        "name": result.name,
        "passed": result.passed,
        "total_turns": result.total_turns,
        "passed_turns": result.passed_turns,
        "failed_turns": result.failed_turns,
        "duration": result.duration_seconds,
        "result": result
    }


def main():
    """Run all tough client tests."""
    print("=" * 70)
    print("TOUGH CLIENT TESTS FOR WHATSAPP BOT")
    print("=" * 70)
    print(f"Next Saturday: {SATURDAY}")
    print(f"Tomorrow: {TOMORROW}")
    print()

    # Use port 5082 as that's where the API is running
    config = TestConfig(
        bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
        mock_server_url="http://localhost:8080",
        response_timeout=60,  # Longer timeout for LLM responses
        poll_interval=0.5
    )

    all_tests = [
        TEST_1_INVALID_RICES,
        TEST_2_LARGE_GROUP,
        TEST_3_TWO_RICE_TYPES,
        TEST_4_SIMPLE_SUCCESS,
        TEST_5_COMBINATION_CHAOS
    ]

    # Check if specific test requested
    if len(sys.argv) > 1:
        test_num = int(sys.argv[1])
        if 1 <= test_num <= 5:
            all_tests = [all_tests[test_num - 1]]
            print(f"Running only Test {test_num}")
        else:
            print(f"Invalid test number: {test_num}. Running all tests.")

    try:
        summary = run_test_suite(all_tests, config)

        print("\n" + "=" * 70)
        print("FINAL SUMMARY")
        print("=" * 70)
        print(f"Total Tests: {summary['total_tests']}")
        print(f"Passed: {summary['passed_tests']}")
        print(f"Failed: {summary['failed_tests']}")
        print(f"Pass Rate: {summary['pass_rate']*100:.1f}%")
        print("=" * 70)

        # Return exit code based on results
        sys.exit(0 if summary["failed_tests"] == 0 else 1)

    except ConnectionError as e:
        print(f"\nConnection Error: {e}")
        print("\nMake sure both servers are running:")
        print("  1. Mock UAZAPI: cd testing && source venv/bin/activate && python mock_uazapi_server.py")
        print("  2. Bot API: cd src/BotGenerator.Api && WHATSAPP_API_URL=http://localhost:8080 dotnet run")
        sys.exit(1)


if __name__ == "__main__":
    main()
