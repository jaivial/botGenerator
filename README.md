# BotGenerator - WhatsApp Restaurant Bot

A multi-tenant WhatsApp bot system for restaurant reservations and customer service, powered by Google Gemini AI.

## Project Structure

```
botGenerator/
├── src/
│   ├── BotGenerator.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/           # API Controllers (WebhookController)
│   │   ├── Program.cs             # Application entry point
│   │   └── appsettings.json       # Configuration
│   ├── BotGenerator.Core/         # Core business logic
│   │   ├── Agents/                # AI Agent implementations
│   │   ├── Services/              # Service layer (Gemini, WhatsApp, etc.)
│   │   ├── Handlers/              # Message and event handlers
│   │   └── Models/                # Domain models and DTOs
│   └── BotGenerator.Prompts/      # Prompt templates by restaurant
│       ├── restaurants/
│       │   ├── villacarmen/       # Villa Carmen specific prompts
│       │   └── example-restaurant/
│       └── shared/                # Shared prompt templates
├── tests/
│   ├── BotGenerator.Core.Tests/   # Unit tests
│   └── BotGenerator.Integration.Tests/  # Integration tests
└── BotGenerator.sln               # Solution file
```

## Prerequisites

- .NET 8.0 SDK
- Redis (for conversation state management)
- Google AI Studio API Key (Gemini)
- UAzapi WhatsApp Business API access

## Setup

1. Clone the repository
2. Install dependencies:
   ```bash
   dotnet restore
   ```

3. Configure environment variables:
   - Copy `.env.example` to `.env`
   - Edit `.env` and add your API keys:
   ```bash
   GOOGLE_AI_API_KEY=your_google_ai_api_key_here
   WHATSAPP_API_URL=https://your-instance.uazapi.com
   WHATSAPP_TOKEN=your_uazapi_token_here
   ```

4. Start Redis:
   ```bash
   docker run -d -p 6379:6379 redis:latest
   ```

5. Build and run:
   ```bash
   dotnet build
   dotnet run --project src/BotGenerator.Api
   ```

### Environment Variables

The application reads configuration from the `.env` file:

| Variable | Description | Required |
|----------|-------------|----------|
| `GOOGLE_AI_API_KEY` | Google AI Studio API key for Gemini | Yes |
| `WHATSAPP_API_URL` | UAzapi instance URL | Yes |
| `WHATSAPP_TOKEN` | UAzapi authentication token | Yes |
| `REDIS_CONNECTION_STRING` | Redis connection string | No (defaults to localhost:6379) |

## Features (Planned)

- Multi-tenant restaurant bot support
- Conversation state management with Redis
- Google Gemini AI integration
- WhatsApp Business API integration via UAzapi
- Restaurant-specific prompt customization
- Reservation handling
- Menu inquiries
- Customer service automation

## API Endpoints

- `GET /api/webhook/health` - Health check endpoint
- `POST /api/webhook/whatsapp-webhook` - WhatsApp webhook handler (to be implemented)

## Development Status

This project is in initial setup phase. Core infrastructure completed:
- ✅ Solution and project structure
- ✅ NuGet package dependencies
- ✅ Configuration system
- ✅ Basic API structure
- ⏳ Service implementations (next steps)
- ⏳ Agent implementations (next steps)
- ⏳ WhatsApp integration (next steps)

## Testing

Run all tests:
```bash
dotnet test
```

## License

[Your License Here]
