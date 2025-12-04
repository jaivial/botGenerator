#!/usr/bin/env python3
"""Test booking with non-existent rice type"""

import sys
sys.path.insert(0, '/home/jaime/Documents/projects/botGenerator/testing')

from conversation_tester import ConversationTester, TestConfig

config = TestConfig(
    bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
    mock_server_url="http://localhost:8080",
    default_phone="34612345679",  # Different phone to avoid state conflicts
    response_timeout=30
)

print("="*60)
print("TEST: Booking with NON-EXISTENT rice type (arroz de pollo)")
print("="*60)
print(f"Bot URL: {config.bot_webhook_url}")
print(f"Mock Server: {config.mock_server_url}")

try:
    tester = ConversationTester(config)
except ConnectionError as e:
    print(f"Connection error: {e}")
    sys.exit(1)

# Clear any previous state
tester.clear_state()

# Run booking conversation with invalid rice
result = tester.run_conversation(
    name="Booking: Invalid Rice Type (arroz de pollo)",
    turns=[
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
            "input": "Quiero arroz de pollo para 2 raciones",
            "expected_contains": []
        }
    ],
    clear_before=False
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
print(f"Test completed in {result.duration_seconds:.1f}s")
print("="*60)
