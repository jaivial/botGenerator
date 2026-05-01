# BotGenerator Documentation

## Version 3.0.0 - Simplified Agent Architecture

This is the simplified WhatsApp reservation bot for Alquería Villa Carmen.

---

## Quick Start

### Run the Bot

```bash
cd bot
dotnet run --project src/BotGenerator.Api
```

### Test the Webhook

```bash
curl -X POST http://localhost:5000/api/webhook/webhook \
  -H "Content-Type: application/json" \
  -d '{
    "EventType": "messages",
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "pushname": "Test User",
      "text": "Hola"
    }
  }'
```

### Health Check

```bash
curl http://localhost:5000/api/webhook/health
```

---

## Architecture

### Single AI Agent with Tool Calls

```
WhatsApp → /api/webhook/webhook → AgentOrchestrator → Tools → Response
```

The bot uses a **single AI agent** that decides which tools to use based on the user's message.

### Available Tools

| Tool | Purpose |
|------|---------|
| `send_message` | Send WhatsApp message |
| `fetch_whatsapp_history` | Get conversation history from UAZAPI |
| `check_future_booking` | Check if user has future bookings |
| `check_day_capacity` | Quick day fullness check |
| `check_availability_for_party` | Check if party fits on date |
| `get_opening_hours_with_capacity` | Opening hours + capacity |
| `check_hour_capacity` | Hour configuration check |
| `get_bookings` | User's existing bookings |
| `create_booking` | Create reservation |
| `cancel_booking` | Cancel reservation |
| `modify_booking` | Modify reservation |

See [API_DOCUMENTATION.md](API_DOCUMENTATION.md) for full details.

---

## Documentation

| Document | Description |
|----------|-------------|
| [API_DOCUMENTATION.md](API_DOCUMENTATION.md) | Full API reference and tool documentation |
| [REFACTORING_PLAN.md](REFACTORING_PLAN.md) | Migration plan and status |
| [steps/09-webhook-whatsapp.md](steps/09-webhook-whatsapp.md) | Webhook integration guide |
| [guides/whatsapp-bot-csharp-guide.md](guides/whatsapp-bot-csharp-guide.md) | C# implementation guide (legacy) |

---

## Project Structure

```
bot/
├── src/
│   ├── BotGenerator.Api/
│   │   ├── Controllers/
│   │   │   └── WebhookController.cs    # Main webhook endpoint
│   │   └── Program.cs                 # DI configuration
│   │
│   └── BotGenerator.Core/
│       ├── Models/
│       │   ├── NewToolModels.cs       # New tool response models
│       │   └── ...
│       │
│       └── Services/
│           ├── AgentOrchestrator.cs    # AI agent with tool calls
│           ├── ToolExecutor.cs        # Tool execution
│           ├── AgentToolDefinitions.cs # Tool definitions
│           ├── WhatsAppService.cs     # UAZAPI integration
│           └── ...
│
├── tests/
│   ├── BotGenerator.Core.Tests/
│   └── BotGenerator.Integration.Tests/
│
└── docs/
    ├── API_DOCUMENTATION.md
    └── REFACTORING_PLAN.md
```

---

## Configuration

### Environment Variables

| Variable | Description |
|----------|-------------|
| `WHATSAPP_API_URL` | UAZAPI base URL |
| `WHATSAPP_TOKEN` | UAZAPI token |
| `MYSQL_CONNECTION_STRING` | MySQL connection string |
| `MINIMAX_API_KEY` | AI service API key |
| `GOOGLE_AI_API_KEY` | Alternative AI service API key |

### Configuration Files

- `.env` - Environment variables (create from `.env.example`)
- `appsettings.json` - Application settings
- `appsettings.Development.json` - Development settings

---

## Database Tables

| Table | Purpose |
|-------|---------|
| `bookings` | Reservation records |
| `openinghours` | Custom opening hours per date |
| `hour_configuration` | Per-hour capacity configuration |
| `daily_limits` | Daily booking limits |
| `reservation_manager` | Alternative daily limits |

---

## Testing

### Run Tests

```bash
cd bot
dotnet test
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## Deployment

### Build for Production

```bash
cd bot
dotnet publish -c Release -o publish
```

### Deploy

Copy the `publish` folder to your server and run:

```bash
dotnet BotGenerator.Api.dll
```

---

## Changelog

### v3.0.0 (2026-05-01)
- **Simplified architecture**: Single AI agent with tool calls
- **Removed legacy pipeline**: Deleted multi-node pipeline approach
- **Removed local history**: Using UAZAPI history directly
- **New tools**: Added 5 new availability tools
- **Single endpoint**: `POST /api/webhook/webhook`

### v2.0.0 (Previous)
- Multi-node pipeline architecture
- Separate agent and webhook endpoints
- Local conversation history storage

---

## Support

For questions or issues, check the documentation or contact the development team.
