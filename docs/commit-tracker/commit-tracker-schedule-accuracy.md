# Commit Tracker - Schedule Accuracy

Session: 2026-07-31

## Changes

| File | Action | What Done |
|------|--------|-----------|
| src/BotGenerator.Core/Models/RestaurantConfig.cs | edit | Define one default open/closed weekday policy shared by schedule reporting and booking paths. |
| src/BotGenerator.Core/Services/ToolExecutor.cs | edit | Return authoritative default open/closed days without claiming hours or applying unhydrated config fields. Use shared policy in booking checks. |
| src/BotGenerator.Core/Services/BookingAvailabilityService.cs | edit | Use shared default closed-day policy. |
| src/BotGenerator.Core/Services/AgentToolDefinitions.cs | edit | Narrow restaurant info wording to default open/closed days and require dated day-status checks. |
| tests/BotGenerator.Core.Tests/Services/RestaurantScheduleToolTests.cs | add | Cover default policy, ignored hypothetical config overrides, absence of claimed hours, and dated-tool guidance. |
| docs/commit-tracker/commit-tracker-schedule-accuracy.md | add | Track schedule accuracy changes and verification. |
| deploy/evolution-staging/compose.yaml | edit | Pin verified ACK-fix image. |
| docs/testing/whatsapp-ack-schedule-fix-verification-2026-07-31.md | add | Record deployment and live checks. |

## Verification

| Command | Result |
|---------|--------|
| `dotnet test tests/BotGenerator.Core.Tests --filter FullyQualifiedName~RestaurantScheduleToolTests --no-restore` | Passed: 10 |
| `dotnet test --no-restore` | Passed: 186 (180 core, 6 integration) |
| `git diff --check` | Passed |
| Evolution live ACK canary | Passed: 20 messages |
| Live schedule matrix | Passed: 5 scenarios |
