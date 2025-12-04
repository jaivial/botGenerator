#!/bin/bash
# Run Conversation Tests

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Activate virtual environment
if [ -d "venv" ]; then
    source venv/bin/activate
else
    echo "Virtual environment not found. Run start_mock_server.sh first to set it up."
    exit 1
fi

# Pass all arguments to the test runner
python run_tests.py "$@"
