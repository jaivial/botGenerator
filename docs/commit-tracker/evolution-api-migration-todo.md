# Evolution API Migration Todo

## Phase 1: Audit

- [x] Map UAZAPI service, webhook, booking flows, and test gaps.
- [x] Verify Evolution licensing, self-hosting, and button risks.
- [x] Isolate existing unrelated Evolution deployment.

## Phase 2: Staging Infrastructure

- [x] Add dedicated Evolution, PostgreSQL, and Redis Compose stack.
- [x] Retain existing `/api/bot/` Nginx forwarding; do not add Evolution route before Phase 3 webhook endpoint.
- [x] Add backup and connection-health scripts.
- [x] Start staging stack without linking a WhatsApp number or creating an instance.

## Phase 3: Bot Provider Integration

- [x] Add Evolution configuration and provider selection. UAZAPI remains default; Evolution validates only when selected.
- [x] Add Evolution 2.x transport for text, interactive messages, history, reactions, and contacts.
- [x] Add authenticated Evolution inbound webhook parser and Redis atomic dedupe.
- [x] Preserve UAZAPI provider and webhook route for rollback.
- [x] Add plain booking-policy and cancellation URL fallback before optional interactive buttons.

Phase 3 adds no Evolution instance, QR pairing, webhook configuration, provider cutover, or outbound retry persistence.

## Phase 4: Reliability And Tests

- [x] Persist outbound booking-confirmation state and retry failures.
- [x] Add Evolution request, webhook, and booking-flow tests.
- [x] Correct obsolete test-harness and webhook-route documentation.
- [x] Run unit and integration tests.
- [x] Align mark-read, E.164 normalization, history pagination, buttons, and CALL fallback with Evolution `2.4.0-rc2`.
- [x] Cover every Evolution transport method and interactive webhook shape with mocked tests.
- [x] Add opt-in staging contract script with PostgreSQL status verification.

## Phase 5: Readiness Gate

- [x] Review implementation and deployment security.
- [ ] Validate buttons on Android, iPhone, WhatsApp Web, and Business.
- [x] Link dedicated staging WhatsApp number and execute RC2 text/button smoke tests with ACK verification.
- [ ] Run documented RC2 contract script against activated staging, including optional real inbound-message phase.
- [ ] Approve production cutover only after all staging checks pass.

## Deferred Production Cutover

- [ ] Apply outbox schema before enabling durable retries.
- [ ] Schedule production-number QR pairing window.
- [ ] Switch provider after verified staging results.
- [ ] Monitor real booking confirmations and retain rollback path.
