# Commit Tracker - f8ea3f6a-b5de-4f86-ac8b-cace28163417

Session: 2026-07-31

## Changes

| Time | File | Action | What Done |
|------|------|--------|-----------|
| 00:00 | docs/commit-tracker/evolution-api-migration-todo.md | add | Add Evolution migration phases. |
| 00:00 | docs/commit-tracker/commit-tracker-f8ea3f6a-b5de-4f86-ac8b-cace28163417.md | add | Track migration changes. |
| 00:00 | src/BotGenerator.Api/Controllers/WebhookController.cs | edit | Harden Evolution webhook retry path. |
| 00:00 | src/BotGenerator.Core/Services/EvolutionWebhookDedupe.cs | edit | Add claim release completion states. |
| 00:00 | src/BotGenerator.Core/Services/EvolutionWhatsAppService.cs | edit | Send allowed Origin header. |
| 00:00 | tests/BotGenerator.Core.Tests/Controllers/EvolutionWebhookControllerTests.cs | edit | Cover webhook claim states. |
| 00:00 | tests/BotGenerator.Core.Tests/Services/EvolutionWebhookDedupeTests.cs | edit | Update claim test. |
| 00:00 | tests/BotGenerator.Core.Tests/Services/EvolutionWhatsAppServiceTests.cs | edit | Assert Evolution Origin header. |
| 00:00 | src/BotGenerator.Api/Program.cs | edit | Gate outbox worker by schema flag. |
| 00:00 | src/BotGenerator.Api/appsettings.json | edit | Disable outbox until migration. |
| 00:00 | src/BotGenerator.Core/Models/BookingConfirmationOutbox.cs | edit | Add outbox enable option. |
| 00:00 | src/BotGenerator.Core/Services/BookingConfirmationOutboxProcessor.cs | edit | Expose outbox enable state. |
| 00:00 | src/BotGenerator.Core/Services/ToolExecutor.cs | edit | Keep direct send before outbox migration. |
| 00:00 | docs/commit-tracker/evolution-api-migration-todo.md | edit | Record readiness review gate. |
| 00:00 | src/BotGenerator.Core/Services/EvolutionWhatsAppService.cs | edit | Generalize provider version documentation. |
| 00:00 | docs/commit-tracker/evolution-api-migration-todo.md | edit | Record RC2 staging smoke test. |
| 00:00 | deploy/evolution-staging/compose.yaml | add | Add isolated local-only Evolution v2.3.7 staging stack. |
| 00:00 | deploy/evolution-staging/.env.example | add | Document non-secret staging runtime environment fields. |
| 00:00 | deploy/evolution-staging/install.sh | add | Install config and generate root-only runtime credentials. |
| 00:00 | deploy/evolution-staging/scripts/backup.sh | add | Back up Evolution PostgreSQL and instance state without secret output. |
| 00:00 | deploy/evolution-staging/scripts/health.sh | add | Check staging containers and local service connections. |
| 00:00 | deploy/evolution-staging/README.md | add | Document staging operations, backup cron policy, and webhook deferral. |
| 00:00 | docs/commit-tracker/evolution-api-migration-todo.md | update | Record completed Phase 2 configuration and Nginx deferral. |
| 00:00 | deploy/evolution-staging/compose.yaml | update | Use permitted local Origin in Evolution v2.3.7 API readiness probe. |
| 00:00 | deploy/evolution-staging/scripts/health.sh | update | Send permitted local Origin in host API readiness probe. |
| 00:00 | deploy/evolution-staging/README.md | update | Document v2.3.7 CORS probe requirement. |
| 00:00 | docs/commit-tracker/evolution-api-migration-todo.md | update | Mark isolated staging startup complete without WhatsApp linking. |
| 00:55 | .env.example | update | Document provider selection and non-secret Evolution environment variable names. |
| 00:55 | src/BotGenerator.Api/appsettings.json | update | Add empty Evolution provider settings and inbound dedupe TTL. |
| 00:55 | src/BotGenerator.Api/Program.cs | update | Map sanitized Evolution config, validate selected provider, and select UAZAPI or Evolution transport. |
| 00:55 | src/BotGenerator.Core/Models/EvolutionMessageParser.cs | add | Parse supported Evolution v2.3.7 inbound and history message content without retaining raw payloads. |
| 00:55 | src/BotGenerator.Core/Services/EvolutionWhatsAppService.cs | add | Implement Evolution v2.3.7 IWhatsAppService transport with API-key auth and response-key validation. |
| 00:55 | src/BotGenerator.Core/Services/EvolutionWebhookDedupe.cs | add | Add Redis atomic inbound message dedupe for Evolution webhooks. |
| 00:55 | src/BotGenerator.Api/Controllers/WebhookController.cs | update | Add authenticated, size-limited Evolution webhook route with instance validation and dedupe-before-processing. |
| 00:55 | src/BotGenerator.Core/Services/ToolExecutor.cs | update | Send plain booking policy and cancellation URLs before optional link buttons. |
| 00:55 | docs/commit-tracker/evolution-api-migration-todo.md | update | Mark Phase 3 integration work complete and record deployment deferrals. |
| 00:55 | docs/commit-tracker/commit-tracker-f8ea3f6a-b5de-4f86-ac8b-cace28163417.md | update | Record Phase 3 touched files. |
| 01:09 | docs/migrations/20260731_booking_confirmation_outbox.sql | add | Add manual MySQL schema for durable booking-confirmation outbox; no runtime DDL. |
| 01:09 | src/BotGenerator.Core/Models/BookingConfirmationOutbox.cs | add | Define outbox state, durable payload, retry options, and backoff policy. |
| 01:09 | src/BotGenerator.Core/Services/IBookingConfirmationOutboxRepository.cs | add | Define idempotent enqueue, lease claim, accepted, and failed state operations. |
| 01:09 | src/BotGenerator.Core/Services/BookingConfirmationOutboxRepository.cs | add | Implement MySQL unique-key enqueue and atomic leased claims. |
| 01:09 | src/BotGenerator.Core/Services/BookingConfirmationPayloadFactory.cs | add | Preserve Spanish confirmation text, plain URLs, and optional link-button payload. |
| 01:09 | src/BotGenerator.Core/Services/BookingConfirmationOutboxProcessor.cs | add | Persist provider acceptance before optional buttons and schedule bounded retries. |
| 01:09 | src/BotGenerator.Api/BookingConfirmationOutboxWorker.cs | add | Retry due confirmation records in hosted worker with restart-safe lease recovery. |
| 01:09 | src/BotGenerator.Api/Program.cs | update | Register outbox repository, processor, options, and hosted retry worker. |
| 01:09 | src/BotGenerator.Api/appsettings.json | update | Configure outbox retry, lease, polling, and batch defaults. |
| 01:09 | src/BotGenerator.Core/Services/ToolExecutor.cs | update | Enqueue booking confirmation after commit and report provider acceptance truthfully. |
| 01:09 | tests/BotGenerator.Core.Tests/Services/BookingCancellationTests.cs | update | Supply outbox dependencies to existing ToolExecutor tests. |
| 01:09 | tests/BotGenerator.Core.Tests/Services/BookingConfirmationOutboxProcessorTests.cs | add | Cover accepted/no-resend, retry, terminal failure, and payload fallback behavior. |
| 01:09 | tests/BotGenerator.Core.Tests/Services/EvolutionWhatsAppServiceTests.cs | add | Cover Evolution headers, request payloads, URL buttons, and error responses. |
| 01:09 | tests/BotGenerator.Core.Tests/Models/EvolutionMessageParserTests.cs | add | Cover Evolution conversation and interactive parser forms. |
| 01:09 | tests/BotGenerator.Core.Tests/Services/EvolutionWebhookDedupeTests.cs | add | Cover Redis-unconfigured safety without a live Redis dependency. |
| 01:09 | tests/BotGenerator.Core.Tests/Controllers/EvolutionWebhookControllerTests.cs | add | Cover provider guard, secret rejection, and duplicate webhook behavior. |
| 01:09 | testing/conversation_tester.py | update | Point default conversation webhook to current API route. |
| 01:09 | testing/run_tests.py | update | Point CLI test default webhook to current API route. |
| 01:09 | testing/bot_state.py | update | Point test state reset route to current API route. |
| 01:09 | testing/run_full_test.py | update | Point full-test health and webhook routes to current API route. |
| 01:09 | testing/run_full_booking_test.py | update | Point booking test webhook to current API route. |
| 01:09 | testing/run_invalid_rice_test.py | update | Point rice-test health and webhook routes to current API route. |
| 01:09 | testing/test_invalid_rice.py | update | Point invalid-rice test webhook to current API route. |
| 01:09 | testing/test_call_rejection.py | update | Point call-rejection test webhook to current API route. |
| 01:09 | testing/test_conversation_quality.py | update | Point quality-test webhook to current API route. |
| 01:09 | testing/tough_client_tests.py | update | Point tough-client test webhook to current API route. |
| 01:09 | testing/quick_booking_test.py | update | Point quick booking test webhook to current API route. |
| 01:09 | testing/run_booking_manual.py | update | Point manual booking test webhook to current API route. |
| 01:09 | testing/run_booking_matrix_tests.py | update | Point booking matrix webhook to current API route. |
| 01:09 | testing/README.md | update | Document current test webhook route. |
| 01:09 | docs/README.md | update | Document current public webhook route. |
| 01:09 | docs/commit-tracker/evolution-api-migration-todo.md | update | Mark Phase 4 reliability and test tasks complete. |
| 01:09 | docs/commit-tracker/commit-tracker-f8ea3f6a-b5de-4f86-ac8b-cace28163417.md | update | Record all Phase 4 changed paths. |
| 01:09 | docs/IMPLEMENTATION_SUMMARY.md | update | Correct obsolete development test state route. |
| 01:09 | docs/API_DOCUMENTATION.md | update | Correct current health and WhatsApp webhook routes. |
| 01:09 | docs/README.md | update | Correct current health and WhatsApp webhook routes. |
| 01:09 | docs/REFACTORING_PLAN.md | update | Replace obsolete endpoint transition notes with current route. |
| 01:09 | docs/step-09-completion-summary.md | update | Correct documented health and WhatsApp webhook routes. |
| 01:09 | docs/steps/00-overview.md | update | Correct webhook architecture diagram route. |
| 01:09 | docs/steps/01-project-setup.md | update | Correct documented health route. |
| 01:09 | docs/steps/09-webhook-whatsapp.md | update | Correct webhook, health, and test state routes. |
| 01:09 | docs/steps/step-01-output.md | update | Correct documented health and WhatsApp webhook routes. |
| 01:09 | docs/steps/11-adding-restaurants.md | update | Correct documented WhatsApp webhook route. |
| 01:09 | docs/guides/whatsapp-bot-csharp-guide.md | update | Correct WhatsApp webhook example route. |
