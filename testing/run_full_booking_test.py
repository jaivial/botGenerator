#!/usr/bin/env python3
"""
Full Booking Test - Tests complete booking flow with valid rice type
This test should reach the final booking insertion in the database.
"""

import subprocess
import time
import sys
import os
import shutil

# Add parent directory for imports
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from conversation_tester import ConversationTester, TestConfig


def setup_test_environment():
    """Set up test environment with mock server and bot"""
    print("=" * 60)
    print("FULL BOOKING TEST (with valid rice)")
    print("=" * 60)
    print()

    # Backup .env (in project root)
    env_path = "../.env"
    env_backup = "../.env.backup"

    print("[SETUP] Backing up .env...")
    if os.path.exists(env_path):
        shutil.copy(env_path, env_backup)

    # Update .env for testing
    print("[SETUP] Switching to test mode (localhost:8080)...")
    with open(env_path, "r") as f:
        content = f.read()

    # Replace production URL with test URL
    if "WHATSAPP_API_URL=" in content:
        lines = content.split("\n")
        new_lines = []
        for line in lines:
            if line.startswith("WHATSAPP_API_URL="):
                new_lines.append("WHATSAPP_API_URL=http://localhost:8080")
            else:
                new_lines.append(line)
        content = "\n".join(new_lines)
    else:
        content += "\nWHATSAPP_API_URL=http://localhost:8080"

    with open(env_path, "w") as f:
        f.write(content)
    print("[SETUP] .env updated for testing")

    # Start mock server
    print("[SETUP] Starting mock UAZAPI server...")
    mock_server = subprocess.Popen(
        ["python", "mock_uazapi_server.py"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE
    )
    time.sleep(2)

    if mock_server.poll() is not None:
        print("[ERROR] Mock server failed to start")
        return None, None, None

    print("[SETUP] Mock server started successfully")

    # Start the bot
    print("[SETUP] Starting bot...")
    bot_process = subprocess.Popen(
        ["dotnet", "run", "--project", "../src/BotGenerator.Api"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE
    )

    print("[SETUP] Waiting for bot to compile and start...")
    time.sleep(25)  # Give bot time to compile and start

    if bot_process.poll() is not None:
        print("[ERROR] Bot failed to start")
        mock_server.terminate()
        return None, None, None

    print("[SETUP] Bot started successfully")
    return mock_server, bot_process, env_backup


def cleanup(mock_server, bot_process, env_backup):
    """Clean up test environment"""
    print("\n[CLEANUP] Stopping processes...")

    if bot_process:
        bot_process.terminate()
        try:
            bot_process.wait(timeout=5)
        except:
            bot_process.kill()

    if mock_server:
        mock_server.terminate()
        try:
            mock_server.wait(timeout=5)
        except:
            mock_server.kill()

    # Restore .env
    env_path = "../src/BotGenerator.Api/.env"
    if env_backup and os.path.exists(env_backup):
        print("[CLEANUP] Restoring original .env...")
        shutil.move(env_backup, env_path)
        print("[CLEANUP] .env restored")


def run_full_booking_test():
    """Run a complete booking test with valid rice type"""
    config = TestConfig(
        bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
        response_timeout=30,
        logs_dir="logs"
    )
    tester = ConversationTester(config)

    # Full booking conversation with valid rice (Arroz de chorizo)
    test_scenario = {
        "name": "Full Booking with Valid Rice (Arroz de chorizo)",
        "phone": "34612345680",
        "turns": [
            {
                "input": "Hola, quiero hacer una reserva",
                "expected_contains": ["reserva"],
                "expected_not_contains": ["error"]
            },
            {
                "input": "Para 4 personas el sabado",
                "expected_contains": ["sábado", "diciembre"],
                "expected_not_contains": ["error"]
            },
            {
                "input": "A las 14:00",
                "expected_contains": ["arroz"],  # Should ask about rice
                "expected_not_contains": ["error"]
            },
            {
                "input": "Si, queremos arroz de chorizo para 4 raciones",
                "expected_contains": ["chorizo"],  # Should confirm the valid rice
                "expected_not_contains": ["no tenemos", "no existe"]
            },
            {
                "input": "Mi nombre es Juan Garcia",
                "expected_contains": [],  # Should ask for confirmation or more details
                "expected_not_contains": ["error"]
            },
            {
                "input": "Si, confirmo la reserva",
                "expected_contains": ["reserva", "confirmad"],  # Should confirm booking
                "expected_not_contains": ["error"]
            }
        ]
    }

    print("\n" + "=" * 60)
    print("RUNNING FULL BOOKING TEST (with valid rice: chorizo)")
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


def main():
    mock_server = None
    bot_process = None
    env_backup = None

    try:
        mock_server, bot_process, env_backup = setup_test_environment()

        if mock_server is None:
            print("[ERROR] Setup failed")
            return 1

        result = run_full_booking_test()
        return 0 if result.passed else 1

    except Exception as e:
        print(f"[ERROR] Test failed with exception: {e}")
        import traceback
        traceback.print_exc()
        return 1

    finally:
        cleanup(mock_server, bot_process, env_backup)


if __name__ == "__main__":
    exit(main())
