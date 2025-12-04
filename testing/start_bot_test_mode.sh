#!/bin/bash
# Start the Bot in Test Mode (using mock UAZAPI server)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "Starting Bot in TEST MODE"
echo "Using Mock UAZAPI at http://localhost:8080"
echo ""

# Export the mock server URL
export WHATSAPP_API_URL="http://localhost:8080"
export WHATSAPP_TOKEN="test-token"

# Start the bot
dotnet run --project src/BotGenerator.Api
