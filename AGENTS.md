# Repository Guidelines

## Project Structure & Module Organization

- `src/BotGenerator.Api/`: ASP.NET Core Web API entrypoint (`Program.cs`) and webhook controller(s).
- `src/BotGenerator.Core/`: business logic (agents, handlers, services, domain models).
- `src/BotGenerator.Prompts/`: prompt templates copied into the API output at build time (see `src/BotGenerator.Api/BotGenerator.Api.csproj`).
- `tests/`: C# unit/integration tests (`BotGenerator.Core.Tests`, `BotGenerator.Integration.Tests`).
- `testing/`: Python black-box conversation test harness + mock UAZAPI server.
- `docs/`: implementation notes, test plans, and workflow documentation.

## Build, Test, and Development Commands

```bash
dotnet restore               # restore NuGet packages
dotnet build                 # compile the solution
dotnet run --project src/BotGenerator.Api   # run the API locally
dotnet test                  # run all C# tests (xUnit)
dotnet test --filter "FullyQualifiedName~ModificationFlow"  # run a subset
```

Conversation E2E tests (mock WhatsApp provider):
```bash
cd testing
./start_mock_server.sh       # creates venv/, installs deps, starts mock server :8080
./start_bot_test_mode.sh     # runs API with WHATSAPP_API_URL=http://localhost:8080
./run_conversation_tests.sh --category booking
```

## Coding Style & Naming Conventions

- C# targets `.NET 8` with `Nullable` and `ImplicitUsings` enabled.
- Indentation: 4 spaces; public types/methods `PascalCase`; locals/parameters `camelCase`.
- Tests follow `Scenario_ExpectedResult` naming (see `tests/BotGenerator.Core.Tests/Conversations/*`).

## Testing Guidelines

- Frameworks: xUnit (`[Fact]`), with `FluentAssertions` and `Moq` in core tests.
- Prefer fast unit tests in `tests/BotGenerator.Core.Tests/` for agents/handlers/services.
- Use `testing/` scripts for multi-turn conversation behavior and regression coverage.

## Commit & Pull Request Guidelines

- Commit subjects in this repo are short, imperative, and descriptive (examples: `Fix ...`, `Add ...`).
- Avoid “WIP”; include the user-facing behavior change and the subsystem touched (e.g., “Fix date parsing in DateParserAgent”).

PRs should include:
1. What changed and why, plus reproduction steps or the exact test command(s) run.
2. Prompt changes called out explicitly (path under `src/BotGenerator.Prompts/`).
3. Screenshots/log snippets only when behavior is primarily conversational.

## Security & Configuration Tips

- Keep secrets out of git: use `.env.example` as a template; `.env` is ignored by `.gitignore`.
- Do not commit production `appsettings.Production.json`; prefer environment variables for keys/tokens.
