#!/usr/bin/env python3
"""
Test runner for invalid rice type - handles .env switching, server management, and testing.
"""

import subprocess
import time
import os
import sys
import signal
import shutil

PROJECT_DIR = "/home/jaime/Documents/projects/botGenerator"
TESTING_DIR = f"{PROJECT_DIR}/testing"
ENV_FILE = f"{PROJECT_DIR}/.env"
ENV_BACKUP = f"{PROJECT_DIR}/.env.testing.backup"

processes = []

def cleanup():
    """Kill all spawned processes and restore .env"""
    print("\n[CLEANUP] Stopping processes...")
    for p in processes:
        try:
            p.terminate()
            p.wait(timeout=5)
        except:
            try:
                p.kill()
            except:
                pass

    # Restore .env if backup exists
    if os.path.exists(ENV_BACKUP):
        print("[CLEANUP] Restoring original .env...")
        shutil.copy(ENV_BACKUP, ENV_FILE)
        os.remove(ENV_BACKUP)
        print("[CLEANUP] .env restored")

def signal_handler(sig, frame):
    cleanup()
    sys.exit(0)

signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)

def switch_to_test_mode():
    """Backup .env and switch to test mode"""
    print("[SETUP] Backing up .env...")
    shutil.copy(ENV_FILE, ENV_BACKUP)

    print("[SETUP] Switching to test mode (localhost:8080)...")
    with open(ENV_FILE, 'r') as f:
        content = f.read()

    # Find and replace the WHATSAPP_API_URL line
    lines = content.split('\n')
    new_lines = []
    for line in lines:
        if line.startswith('WHATSAPP_API_URL='):
            new_lines.append('WHATSAPP_API_URL=http://localhost:8080')
        else:
            new_lines.append(line)

    with open(ENV_FILE, 'w') as f:
        f.write('\n'.join(new_lines))

    print("[SETUP] .env updated for testing")

def start_mock_server():
    """Start the mock UAZAPI server"""
    print("[SETUP] Starting mock UAZAPI server...")

    # Use the venv python directly
    venv_python = f"{TESTING_DIR}/venv/bin/python"
    mock_script = f"{TESTING_DIR}/mock_uazapi_server.py"

    p = subprocess.Popen(
        [venv_python, mock_script],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=TESTING_DIR
    )
    processes.append(p)

    # Wait for server to start
    time.sleep(3)

    # Check if running
    import requests
    for _ in range(5):
        try:
            resp = requests.get("http://localhost:8080/health", timeout=2)
            if resp.status_code == 200:
                print("[SETUP] Mock server started successfully")
                return True
        except:
            time.sleep(1)

    print("[ERROR] Mock server failed to start")
    return False

def start_bot():
    """Start the bot"""
    print("[SETUP] Starting bot...")

    p = subprocess.Popen(
        ["dotnet", "run", "--project", "src/BotGenerator.Api"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=PROJECT_DIR
    )
    processes.append(p)

    # Wait for bot to start
    print("[SETUP] Waiting for bot to compile and start...")
    import requests
    for i in range(30):  # Wait up to 30 seconds
        try:
            resp = requests.get("http://localhost:5082/api/webhook/health", timeout=2)
            if resp.status_code == 200:
                print("[SETUP] Bot started successfully")
                return True
        except:
            time.sleep(1)

    print("[ERROR] Bot failed to start")
    return False

def run_test():
    """Run the invalid rice test"""
    print("\n" + "="*60)
    print("RUNNING INVALID RICE TEST (arroz de pollo)")
    print("="*60 + "\n")

    sys.path.insert(0, TESTING_DIR)
    from conversation_tester import ConversationTester, TestConfig

    config = TestConfig(
        bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
        mock_server_url="http://localhost:8080",
        default_phone="34612345679",
        response_timeout=30
    )

    tester = ConversationTester(config)
    tester.clear_state()

    # Invalid rice test: arroz de pollo (not in database)
    result = tester.run_conversation(
        name="Booking: Invalid Rice (arroz de pollo - NOT in database)",
        turns=[
            {"input": "Hola, quiero hacer una reserva", "expected_contains": []},
            {"input": "Para 2 personas el sabado", "expected_contains": []},
            {"input": "A las 14:00", "expected_contains": []},
            {"input": "Quiero arroz de pollo para 2 raciones", "expected_contains": []},
        ],
        clear_before=False
    )

    print("\n" + "="*60)
    print("FULL CONVERSATION")
    print("="*60)
    for i, turn in enumerate(result.turns):
        print(f"\n[Turn {i+1}]")
        print(f"  User: {turn.user_input}")
        if turn.bot_response:
            print(f"  Bot: {turn.bot_response.text}")
        else:
            print(f"  Bot: (no response)")

    print("\n" + "="*60)
    print(f"Duration: {result.duration_seconds:.1f}s")
    print("="*60 + "\n")

    return True

def main():
    print("="*60)
    print("INVALID RICE TYPE TEST")
    print("="*60 + "\n")

    try:
        # 1. Switch to test mode
        switch_to_test_mode()

        # 2. Start mock server
        if not start_mock_server():
            cleanup()
            return 1

        # 3. Start bot
        if not start_bot():
            cleanup()
            return 1

        # 4. Run test
        run_test()

        # 5. Cleanup
        cleanup()

        return 0

    except Exception as e:
        print(f"[ERROR] {e}")
        import traceback
        traceback.print_exc()
        cleanup()
        return 1

if __name__ == "__main__":
    sys.exit(main())
