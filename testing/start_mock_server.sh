#!/bin/bash
# Start the Mock UAZAPI Server

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Check if virtual environment exists
if [ ! -d "venv" ]; then
    echo "Creating Python virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
source venv/bin/activate

# Install dependencies
echo "Installing dependencies..."
pip install -q -r requirements.txt

# Start the server
echo ""
echo "Starting Mock UAZAPI Server on http://localhost:8080"
echo "Press Ctrl+C to stop"
echo ""
python mock_uazapi_server.py
