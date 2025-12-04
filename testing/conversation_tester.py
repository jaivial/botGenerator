"""
Conversation Tester for WhatsApp Bot

This module provides utilities to test WhatsApp bot conversations by:
1. Sending simulated incoming messages to the bot's webhook
2. Capturing the bot's responses from the mock UAZAPI server
3. Running multi-turn conversation flows
4. Validating responses against expected patterns

Architecture:
┌──────────────┐   POST webhook    ┌──────────────┐   POST send/text   ┌──────────────┐
│              │ ────────────────► │              │ ─────────────────► │  Mock Uazapi │
│   Tester     │                   │  C# Bot API  │                    │   Server     │
│              │ ◄──────────────────────────────────────────────────── │              │
└──────────────┘      reads captured responses                         └──────────────┘
"""

import requests
import time
import uuid
import json
import os
from pathlib import Path
from datetime import datetime
from typing import Optional, Callable
from dataclasses import dataclass, field


@dataclass
class TestConfig:
    """Configuration for the test environment"""
    bot_webhook_url: str = "http://localhost:5000/api/webhook/whatsapp-webhook"
    mock_server_url: str = "http://localhost:8080"
    default_phone: str = "34612345678"
    response_timeout: int = 30  # seconds to wait for response
    poll_interval: float = 0.3  # seconds between polls
    logs_dir: str = "logs"  # directory to save conversation logs
    enable_logging: bool = True  # whether to save conversation logs


class ConversationLogger:
    """Handles saving conversation logs to files"""

    def __init__(self, logs_dir: str = "logs"):
        self.logs_dir = Path(logs_dir)
        self._ensure_logs_dir()

    def _ensure_logs_dir(self):
        """Create logs directory if it doesn't exist"""
        self.logs_dir.mkdir(parents=True, exist_ok=True)

    def _sanitize_filename(self, name: str) -> str:
        """Sanitize test name for use in filename"""
        # Replace spaces and special chars with underscores
        sanitized = "".join(c if c.isalnum() or c in "-_" else "_" for c in name)
        # Remove consecutive underscores
        while "__" in sanitized:
            sanitized = sanitized.replace("__", "_")
        return sanitized.strip("_").lower()

    def save_conversation(self, result: "ConversationResult") -> str:
        """
        Save a conversation result to a log file.

        Args:
            result: The ConversationResult to save

        Returns:
            Path to the saved log file
        """
        timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        test_name = self._sanitize_filename(result.name)
        filename = f"{timestamp}_{test_name}.log"
        filepath = self.logs_dir / filename

        with open(filepath, "w", encoding="utf-8") as f:
            self._write_header(f, result)
            self._write_conversation(f, result)
            self._write_summary(f, result)

        return str(filepath)

    def _write_header(self, f, result: "ConversationResult"):
        """Write log header"""
        f.write("=" * 70 + "\n")
        f.write(f"CONVERSATION LOG\n")
        f.write("=" * 70 + "\n")
        f.write(f"Test Name:   {result.name}\n")
        f.write(f"Phone:       {result.phone}\n")
        f.write(f"Timestamp:   {datetime.now().isoformat()}\n")
        f.write(f"Duration:    {result.duration_seconds:.2f}s\n")
        f.write(f"Result:      {'PASSED' if result.passed else 'FAILED'}\n")
        f.write("=" * 70 + "\n\n")

    def _write_conversation(self, f, result: "ConversationResult"):
        """Write the conversation turns"""
        f.write("CONVERSATION:\n")
        f.write("-" * 70 + "\n\n")

        for i, turn in enumerate(result.turns, 1):
            # User message
            f.write(f"[Turn {i}] USER:\n")
            f.write(f"  {turn.user_input}\n\n")

            # Bot response
            f.write(f"[Turn {i}] BOT:\n")
            if turn.bot_response:
                # Write full response with indentation
                response_lines = turn.bot_response.text.split("\n")
                for line in response_lines:
                    f.write(f"  {line}\n")

                # Add message type if not plain text
                if turn.bot_response.message_type != "text":
                    f.write(f"\n  [Message Type: {turn.bot_response.message_type}]\n")

                # Add choices/buttons if present
                if turn.bot_response.choices:
                    f.write(f"  [Choices: {', '.join(turn.bot_response.choices)}]\n")

                # Add sections/list if present
                if turn.bot_response.sections:
                    f.write(f"  [Sections: {json.dumps(turn.bot_response.sections, ensure_ascii=False)}]\n")
            else:
                f.write("  [NO RESPONSE RECEIVED]\n")

            f.write("\n")

            # Validation result
            status = "PASSED" if turn.validation_passed else "FAILED"
            f.write(f"[Turn {i}] VALIDATION: {status}\n")

            if turn.expected_contains:
                f.write(f"  Expected to contain: {turn.expected_contains}\n")

            if turn.expected_not_contains:
                f.write(f"  Expected NOT to contain: {turn.expected_not_contains}\n")

            if turn.validation_errors:
                f.write(f"  Errors:\n")
                for error in turn.validation_errors:
                    f.write(f"    - {error}\n")

            f.write("\n" + "-" * 70 + "\n\n")

    def _write_summary(self, f, result: "ConversationResult"):
        """Write summary section"""
        f.write("SUMMARY:\n")
        f.write("=" * 70 + "\n")
        f.write(f"Total Turns:  {result.total_turns}\n")
        f.write(f"Passed:       {result.passed_turns}\n")
        f.write(f"Failed:       {result.failed_turns}\n")
        f.write(f"Pass Rate:    {result.passed_turns/result.total_turns*100:.1f}%\n" if result.total_turns > 0 else "Pass Rate:    N/A\n")
        f.write(f"Final Result: {'PASSED' if result.passed else 'FAILED'}\n")
        f.write("=" * 70 + "\n")


@dataclass
class BotResponse:
    """Represents a response from the bot"""
    text: str
    message_type: str
    phone: str
    timestamp: str
    raw: dict
    choices: Optional[list] = None
    sections: Optional[list] = None


@dataclass
class ConversationTurn:
    """Represents one turn in a conversation"""
    user_input: str
    bot_response: Optional[BotResponse] = None
    expected_contains: list[str] = field(default_factory=list)
    expected_not_contains: list[str] = field(default_factory=list)
    validation_passed: bool = False
    validation_errors: list[str] = field(default_factory=list)


@dataclass
class ConversationResult:
    """Result of running a conversation test"""
    name: str
    phone: str
    turns: list[ConversationTurn]
    passed: bool
    total_turns: int
    passed_turns: int
    failed_turns: int
    duration_seconds: float


class ConversationTester:
    """Main class for testing WhatsApp bot conversations"""

    def __init__(self, config: Optional[TestConfig] = None):
        self.config = config or TestConfig()
        self._verify_servers()
        # Initialize logger if enabled
        if self.config.enable_logging:
            self.logger = ConversationLogger(self.config.logs_dir)
        else:
            self.logger = None

    def _verify_servers(self):
        """Verify both the bot and mock server are running"""
        # Check mock server
        try:
            resp = requests.get(f"{self.config.mock_server_url}/health", timeout=5)
            if resp.status_code != 200:
                raise ConnectionError(f"Mock server returned {resp.status_code}")
        except requests.RequestException as e:
            raise ConnectionError(
                f"Cannot connect to mock UAZAPI server at {self.config.mock_server_url}. "
                f"Start it with: python mock_uazapi_server.py\nError: {e}"
            )

        # Check bot webhook
        try:
            resp = requests.get(
                self.config.bot_webhook_url.replace("/whatsapp-webhook", "/health"),
                timeout=5
            )
            if resp.status_code != 200:
                raise ConnectionError(f"Bot webhook returned {resp.status_code}")
        except requests.RequestException as e:
            raise ConnectionError(
                f"Cannot connect to bot at {self.config.bot_webhook_url}. "
                f"Start your bot with: WHATSAPP_API_URL=http://localhost:8080 dotnet run\nError: {e}"
            )

    def clear_state(self):
        """Clear all captured messages and history from mock server"""
        requests.delete(f"{self.config.mock_server_url}/all")

    def inject_history(self, phone: str, messages: list[dict]):
        """
        Inject message history for a phone number.

        Args:
            phone: Phone number
            messages: List of {"text": "...", "fromMe": bool}
        """
        requests.post(
            f"{self.config.mock_server_url}/history/inject",
            json={"phone": phone, "messages": messages}
        )

    def get_captured_count(self) -> int:
        """Get current count of captured messages"""
        resp = requests.get(f"{self.config.mock_server_url}/captured")
        return resp.json().get("count", 0)

    def get_latest_response(self, count: int = 1) -> list[dict]:
        """Get the latest captured responses"""
        resp = requests.get(
            f"{self.config.mock_server_url}/captured/latest",
            params={"count": count}
        )
        return resp.json().get("messages", [])

    def send_message(
        self,
        text: str,
        phone: Optional[str] = None,
        push_name: str = "Test User",
        message_type: str = "text"
    ) -> bool:
        """
        Send a simulated incoming WhatsApp message to the bot.

        Args:
            text: The message text
            phone: Phone number (uses default if not specified)
            push_name: Display name of the sender
            message_type: Type of message (text, button_response, list_response)

        Returns:
            True if webhook accepted the message
        """
        phone = phone or self.config.default_phone
        message_id = f"test_{uuid.uuid4().hex[:12]}"
        timestamp = int(time.time())

        # Build payload matching UAZAPI webhook format
        payload = {
            "instance": "test-instance",
            "event": "message",
            "message": {
                "id": message_id,
                "chatid": f"{phone}@s.whatsapp.net",
                "senderid": f"{phone}@s.whatsapp.net",
                "text": text,
                "fromMe": False,
                "timestamp": timestamp,
                "pushname": push_name,
                "type": message_type
            }
        }

        # Handle button responses
        if message_type == "button_response":
            payload["message"]["type"] = "ButtonsResponseMessage"
            payload["message"]["vote"] = text

        # Handle list responses
        elif message_type == "list_response":
            payload["message"]["type"] = "ListResponseMessage"
            payload["message"]["content"] = {
                "Response": {
                    "SelectedDisplayText": text
                }
            }

        try:
            resp = requests.post(
                self.config.bot_webhook_url,
                json=payload,
                timeout=self.config.response_timeout
            )
            return resp.status_code == 200
        except requests.RequestException as e:
            print(f"Error sending message: {e}")
            return False

    def wait_for_response(
        self,
        phone: Optional[str] = None,
        timeout: Optional[int] = None
    ) -> Optional[BotResponse]:
        """
        Wait for the bot to respond.

        Args:
            phone: Phone number to filter responses
            timeout: Max seconds to wait

        Returns:
            BotResponse if received, None if timeout
        """
        phone = phone or self.config.default_phone
        timeout = timeout or self.config.response_timeout
        initial_count = self.get_captured_count()

        start_time = time.time()
        while time.time() - start_time < timeout:
            current_count = self.get_captured_count()

            if current_count > initial_count:
                # Get new messages
                new_messages = self.get_latest_response(current_count - initial_count)

                # Filter for our phone
                for msg in new_messages:
                    if msg.get("phone") == phone:
                        return BotResponse(
                            text=msg.get("text", ""),
                            message_type=msg.get("type", "unknown"),
                            phone=msg.get("phone", ""),
                            timestamp=msg.get("timestamp", ""),
                            raw=msg,
                            choices=msg.get("choices"),
                            sections=msg.get("sections")
                        )

            time.sleep(self.config.poll_interval)

        return None

    def send_and_wait(
        self,
        text: str,
        phone: Optional[str] = None,
        **kwargs
    ) -> Optional[BotResponse]:
        """
        Send a message and wait for the response.

        Args:
            text: Message text to send
            phone: Phone number
            **kwargs: Additional args passed to send_message

        Returns:
            BotResponse if received, None if failed or timeout
        """
        phone = phone or self.config.default_phone
        initial_count = self.get_captured_count()

        if not self.send_message(text, phone=phone, **kwargs):
            return None

        # Wait for new message after our count
        timeout = kwargs.get("timeout", self.config.response_timeout)
        start_time = time.time()

        while time.time() - start_time < timeout:
            current_count = self.get_captured_count()

            if current_count > initial_count:
                messages = self.get_latest_response(current_count - initial_count)

                for msg in messages:
                    if msg.get("phone") == phone:
                        return BotResponse(
                            text=msg.get("text", ""),
                            message_type=msg.get("type", "unknown"),
                            phone=msg.get("phone", ""),
                            timestamp=msg.get("timestamp", ""),
                            raw=msg,
                            choices=msg.get("choices"),
                            sections=msg.get("sections")
                        )

            time.sleep(self.config.poll_interval)

        return None

    def validate_response(
        self,
        response: BotResponse,
        expected_contains: list[str] = None,
        expected_not_contains: list[str] = None,
        custom_validator: Callable[[BotResponse], tuple[bool, str]] = None
    ) -> tuple[bool, list[str]]:
        """
        Validate a bot response.

        Args:
            response: The response to validate
            expected_contains: Strings that should be in the response (case-insensitive)
            expected_not_contains: Strings that should NOT be in the response
            custom_validator: Optional function(response) -> (passed, error_msg)

        Returns:
            (passed, list_of_errors)
        """
        errors = []
        response_lower = response.text.lower()

        # Check expected_contains
        if expected_contains:
            for expected in expected_contains:
                if expected.lower() not in response_lower:
                    errors.append(f"Expected '{expected}' not found in response")

        # Check expected_not_contains
        if expected_not_contains:
            for not_expected in expected_not_contains:
                if not_expected.lower() in response_lower:
                    errors.append(f"Unexpected '{not_expected}' found in response")

        # Run custom validator
        if custom_validator:
            try:
                passed, error_msg = custom_validator(response)
                if not passed:
                    errors.append(error_msg)
            except Exception as e:
                errors.append(f"Custom validator error: {e}")

        return len(errors) == 0, errors

    def run_conversation(
        self,
        name: str,
        turns: list[dict],
        phone: Optional[str] = None,
        clear_before: bool = True
    ) -> ConversationResult:
        """
        Run a multi-turn conversation test.

        Args:
            name: Name of the test
            turns: List of turn definitions:
                [
                    {
                        "input": "Hola",
                        "expected_contains": ["bienvenido"],
                        "expected_not_contains": ["error"]
                    },
                    ...
                ]
            phone: Phone number to use
            clear_before: Whether to clear state before running

        Returns:
            ConversationResult with all details
        """
        phone = phone or self.config.default_phone
        start_time = time.time()

        if clear_before:
            self.clear_state()

        completed_turns = []
        all_passed = True

        print(f"\n{'='*60}")
        print(f"Running: {name}")
        print(f"Phone: {phone}")
        print(f"{'='*60}")

        for i, turn_def in enumerate(turns):
            user_input = turn_def.get("input", "")
            expected_contains = turn_def.get("expected_contains", [])
            expected_not_contains = turn_def.get("expected_not_contains", [])

            print(f"\n[Turn {i+1}] User: {user_input}")

            # Send message and wait for response
            response = self.send_and_wait(user_input, phone=phone)

            turn = ConversationTurn(
                user_input=user_input,
                expected_contains=expected_contains,
                expected_not_contains=expected_not_contains
            )

            if response:
                turn.bot_response = response
                print(f"[Turn {i+1}] Bot: {response.text[:100]}{'...' if len(response.text) > 100 else ''}")

                # Validate
                passed, errors = self.validate_response(
                    response,
                    expected_contains=expected_contains,
                    expected_not_contains=expected_not_contains
                )
                turn.validation_passed = passed
                turn.validation_errors = errors

                if not passed:
                    all_passed = False
                    print(f"[Turn {i+1}] FAILED: {errors}")
                else:
                    print(f"[Turn {i+1}] PASSED")
            else:
                turn.validation_passed = False
                turn.validation_errors = ["No response received from bot"]
                all_passed = False
                print(f"[Turn {i+1}] FAILED: No response")

            completed_turns.append(turn)

        duration = time.time() - start_time
        passed_count = sum(1 for t in completed_turns if t.validation_passed)

        print(f"\n{'='*60}")
        print(f"Result: {'PASSED' if all_passed else 'FAILED'}")
        print(f"Turns: {passed_count}/{len(completed_turns)} passed")
        print(f"Duration: {duration:.2f}s")
        print(f"{'='*60}\n")

        result = ConversationResult(
            name=name,
            phone=phone,
            turns=completed_turns,
            passed=all_passed,
            total_turns=len(completed_turns),
            passed_turns=passed_count,
            failed_turns=len(completed_turns) - passed_count,
            duration_seconds=duration
        )

        # Save conversation log if logging is enabled
        if self.logger:
            log_path = self.logger.save_conversation(result)
            print(f"Log saved to: {log_path}")

        return result


def run_test_suite(
    test_scenarios: list[dict],
    config: Optional[TestConfig] = None
) -> dict:
    """
    Run a suite of conversation tests.

    Args:
        test_scenarios: List of test definitions:
            [
                {
                    "name": "Basic Greeting",
                    "phone": "optional",
                    "turns": [...]
                },
                ...
            ]
        config: Test configuration

    Returns:
        Summary dict with results
    """
    tester = ConversationTester(config)
    results = []

    for scenario in test_scenarios:
        result = tester.run_conversation(
            name=scenario.get("name", "Unnamed Test"),
            turns=scenario.get("turns", []),
            phone=scenario.get("phone"),
            clear_before=scenario.get("clear_before", True)
        )
        results.append(result)

    # Summary
    total_tests = len(results)
    passed_tests = sum(1 for r in results if r.passed)

    print("\n" + "=" * 60)
    print("TEST SUITE SUMMARY")
    print("=" * 60)
    for r in results:
        status = "PASSED" if r.passed else "FAILED"
        print(f"  [{status}] {r.name} ({r.passed_turns}/{r.total_turns} turns)")
    print("-" * 60)
    print(f"TOTAL: {passed_tests}/{total_tests} tests passed")
    print("=" * 60)

    return {
        "total_tests": total_tests,
        "passed_tests": passed_tests,
        "failed_tests": total_tests - passed_tests,
        "pass_rate": passed_tests / total_tests if total_tests > 0 else 0,
        "results": results
    }


# Example usage
if __name__ == "__main__":
    # Example test scenarios
    example_scenarios = [
        {
            "name": "Basic Greeting Flow",
            "turns": [
                {
                    "input": "Hola",
                    "expected_contains": ["hola", "bienvenido"]
                },
                {
                    "input": "Quiero hacer una reserva",
                    "expected_contains": ["reserva"]
                }
            ]
        },
        {
            "name": "Booking Flow",
            "turns": [
                {
                    "input": "Quiero reservar para 4 personas manana a las 8pm",
                    "expected_contains": ["personas", "reserva"]
                }
            ]
        }
    ]

    print("Starting conversation tests...")
    print("Make sure both servers are running:")
    print("  1. Mock UAZAPI: python mock_uazapi_server.py")
    print("  2. Bot: WHATSAPP_API_URL=http://localhost:8080 dotnet run")
    print()

    try:
        summary = run_test_suite(example_scenarios)
        exit(0 if summary["failed_tests"] == 0 else 1)
    except ConnectionError as e:
        print(f"Connection Error: {e}")
        exit(1)
