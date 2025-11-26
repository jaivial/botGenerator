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

3. Configure API keys in `appsettings.json` or `appsettings.Development.json`:
   - GoogleAI:ApiKey - Your Google AI Studio API key
   - WhatsApp:Token - Your UAzapi token
   - WhatsApp:ApiUrl - Your UAzapi instance URL

4. Start Redis:
   ```bash
   docker run -d -p 6379:6379 redis:latest
   ```

5. Build and run:
   ```bash
   dotnet build
   dotnet run --project src/BotGenerator.Api
   ```

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
