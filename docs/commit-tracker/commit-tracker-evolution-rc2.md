# Commit Tracker - evolution-rc2

Session: 2026-07-31 10:45 UTC

## Changes

| Time | File | Action | What Done |
|------|------|--------|-----------|
| 10:50 | deploy/evolution-staging/compose.yaml | edit | Pin official rc2 digest. Update readiness route. |
| 10:50 | deploy/evolution-staging/scripts/health.sh | edit | Verify licensed business endpoint safely. |
| 10:50 | deploy/evolution-staging/README.md | edit | Document rc2 activation and validation. |
| 10:50 | docs/commit-tracker/commit-tracker-evolution-rc2.md | add | Track staging rc2 validation changes. |
| 10:56 | deploy/evolution-staging/compose.yaml | edit | Restore rc2 root readiness probe. |
| 10:56 | deploy/evolution-staging/scripts/backup.sh | edit | Make backup checksums relocatable. |
| 10:56 | deploy/evolution-staging/scripts/health.sh | edit | Send allowed origin for rc2 checks. |
| 10:56 | deploy/evolution-staging/README.md | edit | Correct rc2 public readiness route. |
| 11:01 | deploy/evolution-staging/README.md | edit | Record inactive license validation result. |
| 11:01 | docs/commit-tracker/commit-tracker-evolution-rc2.md | edit | Track final validation result. |
| 12:00 | src/BotGenerator.Core/Services/EvolutionWhatsAppService.cs | edit | Fix RC2 contracts, numbers, and pagination. |
| 12:00 | src/BotGenerator.Core/Services/IWhatsAppService.cs | edit | Correct mark-read contract comment. |
| 12:00 | src/BotGenerator.Api/Controllers/WebhookController.cs | edit | Add Evolution CALL reply fallback. |
| 12:00 | tests/BotGenerator.Core.Tests/Services/EvolutionWhatsAppServiceTests.cs | edit | Cover all Evolution transport contracts. |
| 12:00 | tests/BotGenerator.Core.Tests/Models/EvolutionMessageParserTests.cs | edit | Split interactive parser coverage. |
| 12:00 | tests/BotGenerator.Core.Tests/Controllers/EvolutionWebhookControllerTests.cs | edit | Test interactive webhooks and CALL fallback. |
| 12:00 | deploy/evolution-staging/scripts/contract-test.sh | add | Add safe live RC2 contract checks. |
| 12:00 | deploy/evolution-staging/install.sh | edit | Install contract test script. |
| 12:00 | deploy/evolution-staging/README.md | edit | Document live contract test workflow. |
| 12:00 | README.md | edit | Document Evolution RC2 test path. |
| 12:00 | docs/commit-tracker/evolution-api-migration-todo.md | edit | Track RC2 contract hardening. |
| 12:00 | docs/commit-tracker/commit-tracker-evolution-rc2.md | edit | Track RC2 contract changes. |
