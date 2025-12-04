#!/usr/bin/env python3
"""
Test Runner for WhatsApp Bot Conversations

Usage:
    python run_tests.py                    # Run all tests
    python run_tests.py --category booking # Run specific category
    python run_tests.py --list             # List test categories
    python run_tests.py --interactive      # Interactive mode

Prerequisites:
    1. Start the mock UAZAPI server:
       python mock_uazapi_server.py

    2. Start your bot with mock URL:
       WHATSAPP_API_URL=http://localhost:8080 dotnet run --project src/BotGenerator.Api
"""

import argparse
import sys
import json
from datetime import datetime

from conversation_tester import ConversationTester, TestConfig, run_test_suite
from test_scenarios import (
    get_all_tests,
    get_tests_by_category,
    list_categories
)


def interactive_mode(tester: ConversationTester):
    """Run interactive conversation testing"""
    print("\n" + "=" * 60)
    print("INTERACTIVE CONVERSATION MODE")
    print("=" * 60)
    print("Type messages to send to the bot.")
    print("Commands:")
    print("  /clear  - Clear conversation state")
    print("  /phone <number> - Change phone number")
    print("  /quit   - Exit interactive mode")
    print("=" * 60 + "\n")

    phone = tester.config.default_phone
    tester.clear_state()

    while True:
        try:
            user_input = input(f"You ({phone[-4:]}): ").strip()

            if not user_input:
                continue

            if user_input.startswith("/"):
                cmd = user_input.lower().split()
                if cmd[0] == "/quit":
                    print("Exiting interactive mode.")
                    break
                elif cmd[0] == "/clear":
                    tester.clear_state()
                    print("[State cleared]")
                    continue
                elif cmd[0] == "/phone" and len(cmd) > 1:
                    phone = cmd[1]
                    print(f"[Phone changed to {phone}]")
                    continue
                else:
                    print("Unknown command. Use /quit, /clear, or /phone <number>")
                    continue

            # Send message and wait for response
            response = tester.send_and_wait(user_input, phone=phone)

            if response:
                print(f"Bot: {response.text}")
                if response.choices:
                    print(f"     [Choices: {response.choices}]")
            else:
                print("[No response received]")

        except KeyboardInterrupt:
            print("\nExiting...")
            break


def single_test_mode(tester: ConversationTester, test_name: str):
    """Run a single test by name"""
    all_tests = get_all_tests()
    matching = [t for t in all_tests if test_name.lower() in t["name"].lower()]

    if not matching:
        print(f"No test found matching '{test_name}'")
        print("\nAvailable tests:")
        for t in all_tests:
            print(f"  - {t['name']}")
        return 1

    summary = run_test_suite(matching)
    return 0 if summary["failed_tests"] == 0 else 1


def main():
    parser = argparse.ArgumentParser(
        description="Run WhatsApp bot conversation tests",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )

    parser.add_argument(
        "--category", "-c",
        help="Run tests from specific category"
    )
    parser.add_argument(
        "--list", "-l",
        action="store_true",
        help="List available test categories"
    )
    parser.add_argument(
        "--interactive", "-i",
        action="store_true",
        help="Run in interactive mode"
    )
    parser.add_argument(
        "--test", "-t",
        help="Run a specific test by name (partial match)"
    )
    parser.add_argument(
        "--bot-url",
        default="http://localhost:5000/api/webhook/whatsapp-webhook",
        help="Bot webhook URL"
    )
    parser.add_argument(
        "--mock-url",
        default="http://localhost:8080",
        help="Mock UAZAPI server URL"
    )
    parser.add_argument(
        "--phone",
        default="34612345678",
        help="Default phone number for tests"
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=30,
        help="Response timeout in seconds"
    )
    parser.add_argument(
        "--output", "-o",
        help="Output results to JSON file"
    )

    args = parser.parse_args()

    # List categories
    if args.list:
        print("Available test categories:")
        for cat in list_categories():
            tests = get_tests_by_category(cat)
            print(f"  {cat}: {len(tests)} tests")
        return 0

    # Create config
    config = TestConfig(
        bot_webhook_url=args.bot_url,
        mock_server_url=args.mock_url,
        default_phone=args.phone,
        response_timeout=args.timeout
    )

    # Try to connect
    try:
        tester = ConversationTester(config)
    except ConnectionError as e:
        print(f"\nError: {e}")
        print("\nMake sure both servers are running:")
        print("  1. python mock_uazapi_server.py")
        print("  2. WHATSAPP_API_URL=http://localhost:8080 dotnet run --project src/BotGenerator.Api")
        return 1

    # Interactive mode
    if args.interactive:
        interactive_mode(tester)
        return 0

    # Single test
    if args.test:
        return single_test_mode(tester, args.test)

    # Get tests to run
    if args.category:
        tests = get_tests_by_category(args.category)
        if not tests:
            print(f"Unknown category: {args.category}")
            print(f"Available: {', '.join(list_categories())}")
            return 1
    else:
        tests = get_all_tests()

    # Run tests
    print(f"\nRunning {len(tests)} tests...")
    print(f"Bot URL: {config.bot_webhook_url}")
    print(f"Mock URL: {config.mock_server_url}")
    print(f"Phone: {config.default_phone}")
    print()

    summary = run_test_suite(tests, config)

    # Save results if output specified
    if args.output:
        output_data = {
            "timestamp": datetime.now().isoformat(),
            "config": {
                "bot_url": config.bot_webhook_url,
                "mock_url": config.mock_server_url,
                "phone": config.default_phone
            },
            "summary": {
                "total": summary["total_tests"],
                "passed": summary["passed_tests"],
                "failed": summary["failed_tests"],
                "pass_rate": summary["pass_rate"]
            },
            "results": [
                {
                    "name": r.name,
                    "passed": r.passed,
                    "turns": [
                        {
                            "input": t.user_input,
                            "response": t.bot_response.text if t.bot_response else None,
                            "passed": t.validation_passed,
                            "errors": t.validation_errors
                        }
                        for t in r.turns
                    ]
                }
                for r in summary["results"]
            ]
        }

        with open(args.output, "w") as f:
            json.dump(output_data, f, indent=2, ensure_ascii=False)
        print(f"\nResults saved to: {args.output}")

    return 0 if summary["failed_tests"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
