#!/usr/bin/env python3
"""Quick booking test - 2 people, Saturday, chorizo rice x2"""

import sys
sys.path.insert(0, '/home/jaime/Documents/projects/botGenerator/testing')

from conversation_tester import ConversationTester, TestConfig

config = TestConfig(
    bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
    mock_server_url="http://localhost:8080",
    default_phone="34612345678",
    response_timeout=30
)

print("Starting booking test...")
print(f"Bot URL: {config.bot_webhook_url}")
print(f"Mock Server: {config.mock_server_url}")

try:
    tester = ConversationTester(config)
except ConnectionError as e:
    print(f"Connection error: {e}")
    sys.exit(1)

# Clear any previous state
tester.clear_state()

# Run booking conversation
booking_test = {
    "name": "Booking: 2 people, Saturday, Chorizo Rice x2",
    "turns": [
        {
            "input": "Hola, quiero hacer una reserva",
            "expected_contains": []
        },
        {
            "input": "Para 2 personas el sabado",
            "expected_contains": []
        },
        {
            "input": "A las 14:00",
            "expected_contains": []
        },
        {
            "input": "Quiero pedir arroz de chorizo para 2 raciones",
            "expected_contains": []
        },
        {
            "input": "Mi nombre es Juan Garcia",
            "expected_contains": []
        }
    ]
}

result = tester.run_conversation(
    name=booking_test["name"],
    turns=booking_test["turns"],
    clear_before=False  # Already cleared
)

print(f"\n{'='*60}")
print("FULL CONVERSATION LOG")
print("="*60)
for i, turn in enumerate(result.turns):
    print(f"\n[Turn {i+1}]")
    print(f"  User: {turn.user_input}")
    if turn.bot_response:
        print(f"  Bot: {turn.bot_response.text}")
    else:
        print(f"  Bot: (no response)")

print(f"\n{'='*60}")
print(f"Test Result: {'PASSED' if result.passed else 'FAILED'}")
print(f"Duration: {result.duration_seconds:.1f}s")
print("="*60)
