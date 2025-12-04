# WhatsApp Bot Conversation Testing

This directory contains tools for testing WhatsApp bot conversations using a mock UAZAPI server.

## Architecture

```
┌──────────────┐   POST webhook    ┌──────────────┐   POST send/text   ┌──────────────┐
│              │ ────────────────► │              │ ─────────────────► │  Mock UAZAPI │
│ Test Runner  │                   │  C# Bot API  │                    │    Server    │
│              │ ◄──────────────────────────────────────────────────── │              │
└──────────────┘      reads captured responses                         └──────────────┘
```

## Quick Start

### 1. Start the Mock Server

```bash
./start_mock_server.sh
```

This starts the mock UAZAPI server on `http://localhost:8080`

### 2. Start the Bot in Test Mode

In a new terminal:

```bash
./start_bot_test_mode.sh
```

This starts the bot with `WHATSAPP_API_URL=http://localhost:8080`

### 3. Run Tests

In a third terminal:

```bash
# Run all tests
./run_conversation_tests.sh

# Run specific category
./run_conversation_tests.sh --category booking

# Interactive mode
./run_conversation_tests.sh --interactive

# List categories
./run_conversation_tests.sh --list

# Run single test by name
./run_conversation_tests.sh --test "greeting"

# Save results to JSON
./run_conversation_tests.sh --output results.json
```

## Files

| File | Description |
|------|-------------|
| `mock_uazapi_server.py` | Mock UAZAPI server (FastAPI) |
| `conversation_tester.py` | Test harness library |
| `test_scenarios.py` | Test case definitions |
| `run_tests.py` | CLI test runner |
| `requirements.txt` | Python dependencies |
| `start_mock_server.sh` | Start mock server |
| `start_bot_test_mode.sh` | Start bot in test mode |
| `run_conversation_tests.sh` | Run tests |

## Mock Server API

### UAZAPI Endpoints (mocked)

- `POST /send/text` - Send text message
- `POST /send/menu` - Send buttons/menu
- `POST /message/find` - Get message history

### Test Control Endpoints

- `GET /captured` - Get all captured messages
- `GET /captured/latest?count=N` - Get latest N messages
- `GET /captured/phone/{phone}` - Get messages for phone
- `DELETE /captured` - Clear captured messages
- `DELETE /history` - Clear simulated history
- `DELETE /all` - Clear everything
- `POST /history/inject` - Inject test history
- `GET /health` - Health check

## Writing Tests

Add tests to `test_scenarios.py`:

```python
MY_TESTS = [
    {
        "name": "My Test Case",
        "turns": [
            {
                "input": "Hola",
                "expected_contains": ["bienvenido"],
                "expected_not_contains": ["error"]
            },
            {
                "input": "Quiero reservar",
                "expected_contains": ["reserva"]
            }
        ]
    }
]
```

## Using from Python

```python
from conversation_tester import ConversationTester, TestConfig

config = TestConfig(
    bot_webhook_url="http://localhost:5000/api/webhook/whatsapp-webhook",
    mock_server_url="http://localhost:8080",
    default_phone="34612345678"
)

tester = ConversationTester(config)

# Send a message and get response
response = tester.send_and_wait("Hola")
print(f"Bot said: {response.text}")

# Run a conversation
result = tester.run_conversation(
    name="My Test",
    turns=[
        {"input": "Hola", "expected_contains": ["bienvenido"]},
        {"input": "Reservar para 2", "expected_contains": ["personas"]}
    ]
)

print(f"Passed: {result.passed}")
```

## Test Categories

| Category | Description |
|----------|-------------|
| `greeting` | Greeting messages in different languages |
| `booking` | Booking flow tests |
| `datetime` | Date and time parsing |
| `modification` | Booking modification |
| `cancellation` | Cancellation flow |
| `edge` | Edge cases and special inputs |
| `multiturn` | Multi-turn conversations |
| `special` | Special requests (rice, high chairs, etc.) |
