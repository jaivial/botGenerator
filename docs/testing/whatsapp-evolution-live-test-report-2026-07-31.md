# WhatsApp Evolution Live Test Report

Date: 2026-07-31

## Scope

- Evolution instance: `botgenerator-staging`
- WhatsApp sender: `+34 960 255 536`
- Approved recipient: `+34 692 747 052`
- C# runtime: production `botgenerator.service`
- PHP runtime: `/var/www/alqueriavillacarmen`
- Method: authenticated Evolution webhook payloads plus real outbound WhatsApp sends
- Test booking marker: customer `QA Evolution`

## Summary

| Result | Count |
|---|---:|
| Passed | 36 |
| Failed | 2 |
| Not run or constrained | 8 |

Core lifecycle passed: create, retrieve, confirmation gating, modify, reminder,
cancel, repeat cancel, and database cleanup.

## Executed Tests

| ID | Scenario | Result | Evidence |
|---|---|---|---|
| T01 | Evolution connection state | PASS | Instance returned `open` |
| T02 | PHP text send | PASS | HTTP `201`; message ID returned |
| T03 | PHP URL-button send | PASS | HTTP `201`; message ID returned |
| T04 | C# dotted webhook event | PASS | `messages.upsert` invoked agent and sent one message |
| T05 | Public webhook proxy | PASS | Authenticated public route returned `200` |
| T06 | Wrong webhook secret | PASS | HTTP `401` |
| T07 | Wrong instance name | PASS | HTTP `400` |
| T08 | Group JID | PASS | Returned `processed=false, ignored=true` |
| T09 | LID sender | PASS | Returned `processed=false, ignored=true` |
| T10 | Empty text | PASS | Returned `processed=false, ignored=true` |
| T11 | Unsupported event | PASS | HTTP `200`; ignored |
| T12 | Outbound/from-me event | PASS | HTTP `200`; ignored |
| T13 | Malformed JSON | PASS | HTTP `400` |
| T14 | Body over 64 KiB | PASS | HTTP `413` |
| T15 | Image message | PASS | Text-only fallback sent; `unsupportedContent=true` |
| T16 | Duplicate webhook ID | PASS | First processed; second returned `duplicate=true` |
| T17 | Greeting and capabilities | PASS | Accurate supported-task answer |
| T18 | General opening hours and days | FAIL | Answer contradicted booking calendar; see F01 |
| T19 | Location and contact | PASS | Address, phone, email, and website returned |
| T20 | Rice menu | PASS | `get_rice_menu` returned active options |
| T21 | Prompt injection | PASS | No booking tool called; bypass request refused |
| T22 | Past-date booking | PASS | Rejected; no booking created |
| T23 | Rice portions over party size | PASS | Invalid five portions for two people rejected |
| T24 | Unicode and HTML-like input | PASS | Treated as text; no instruction bypass |
| T25 | Explicitly closed date | PASS | `15/08/2026` rejected |
| T26 | Closed Sunday | PASS | `16/08/2026` rejected |
| T27 | Date with zero capacity | PASS | `01/08/2026` reported full |
| T28 | Valid confirmed creation | PASS | Booking `2750`, `18/09/2026 14:00`, two people |
| T29 | Booking confirmation send | PASS | Evolution accepted confirmation text/button |
| T30 | Retrieve active booking | PASS | Booking `2750` returned to owning phone |
| T31 | Modification without confirmation | PASS | DB stayed at zero high chairs and strollers |
| T32 | Confirmed modification | PASS | DB changed to one high chair and one stroller |
| T33 | Modification history | PASS | One history row created |
| T34 | Reminder send | PASS | HTTP `201`; confirmation button message ID returned |
| T35 | Cancellation without confirmation | PASS | Active booking remained |
| T36 | Confirmed cancellation | PASS | Active row removed; archive recorded `AI_AGENT` |
| T37 | Repeat cancellation and invalid modify ID | PASS | Not-found responses; no false success |
| T38 | Delivery ACK persistence | FAIL | 51 recent records remained `PENDING`; see F02 |

## Failures

### F01: Opening-days answer is inaccurate

Input:

```text
¿Cuál es el horario del restaurante y qué días cerráis?
```

Observed answer claimed every-day opening and normal Sunday closure. Booking logic
uses Monday, Tuesday, and Wednesday as default closed days unless a dated
`restaurant_days` override exists.

Log evidence:

- Agent called `get_restaurant_info` successfully.
- Tool returned contact and menu fields, but no authoritative schedule.
- Model supplied missing schedule details and contradicted availability logic.

Root cause: `ToolExecutor.ExecuteGetRestaurantInfo` does not return opening days
or hours. General schedule answers therefore rely on model inference.

Recommended fix: expose canonical weekly schedule and dated overrides from
`get_restaurant_info`, or require a date and call availability tools before
stating whether restaurant is open.

### F02: Delivery ACK status never advances

Evolution PostgreSQL result for recent recipient messages:

```text
PENDING | 51
```

API calls returned `201`, message IDs appeared in Evolution history, and earlier
contract messages were visually confirmed on recipient handset. Persisted status
did not advance to server, delivery, or read ACK.

Root cause: current Baileys/Evolution build accepts submissions, but receipt
update events are not being persisted for this session.

Impact: database state cannot prove handset delivery.

Recommended fix: capture Baileys `messages.update` and receipt events, then verify
Evolution status mapping. Keep handset rendering checks as release gate meanwhile.

## Booking Lifecycle Evidence

Created record:

```text
id=2750
customer_name=QA Evolution
contact_phone=692747052
reservation_date=2026-09-18
reservation_time=14:00
party_size=2
status=pending
```

Modification:

```text
before: highChairs=0, babyStrollers=0
after:  highChairs=1, babyStrollers=1
modification_history rows=1
```

Cancellation and cleanup:

```text
active row after cancellation=0
archive booking_id=2750, cancelled_by=AI_AGENT
QA Evolution active rows after cleanup=0
QA Evolution cancelled rows after cleanup=0
```

## Scenario Catalog

### Transport And Webhook

- Connected, connecting, closed, logged-out, and reconnecting instance states.
- Valid, invalid, and missing API key or webhook secret.
- Correct, wrong, and missing instance name.
- Dotted, underscored, uppercase, lowercase, and unknown event names.
- Sequential and concurrent duplicate delivery.
- Empty, malformed, truncated, oversized, and deeply nested JSON.
- Text, extended text, button reply, list selection, native flow, image, audio,
  video, document, sticker, location, contact, reaction, and call events.
- Personal, group, broadcast, newsletter, status, LID, and malformed JIDs.
- `fromMe=true`, delayed, replayed, and out-of-order events.
- Evolution timeout or 4xx/5xx; Redis, MySQL, or AI provider outage.

### Booking Creation

- All details in one turn and step-by-step collection.
- Relative, explicit, ambiguous, past, same-day, closed, full, and distant dates.
- 12-hour and 24-hour times, invalid minutes, unavailable slots, alternatives.
- One person, normal party, zero, negative, capacity boundary, over capacity.
- No rice, valid rice, fuzzy name, ambiguous rice, unavailable rice, one portion,
  portions over party size, and multiple rice types.
- Zero, valid, negative, and excessive high chairs or strollers.
- Missing, Unicode, very long, punctuated, and injection-like customer names.
- Explicit confirmation, ambiguous confirmation, rejection, changed details before
  confirmation, repeated confirmation, duplicate request, and webhook replay.
- Provider timeout after DB insert and confirmation-send failure.
- Special menu, restricted date, group menu, dietary note, and long commentary.

### User Requests And Lookup

- No active booking, one booking, and multiple bookings.
- Exact ID, ambiguous selection, ownership mismatch, and foreign booking ID.
- Hours, location, phone, email, website, menu, rice, accessibility, and policies.
- Spanish, English, mixed language, spelling mistakes, emojis, and media fallback.
- Topic switch during booking and later resume.
- Prompt injection, tool-name injection, SQL-like text, HTML-like text, fake IDs.

### Modification

- Without confirmation and with explicit confirmation.
- Date, time, people, rice type, rice portions, remove rice, high chairs, strollers.
- Multiple fields, correction before confirmation, no-op, and missing fields.
- Invalid, past, same-day, closed, full, or unavailable target date/time.
- Party reduction below rice or extras count.
- Rice portions below minimum or over party size.
- Negative or excessive extras.
- Cancelled, missing, past, and foreign booking.
- First through fourth modification-limit attempt.
- Concurrent updates, duplicate confirmed event, DB success plus send failure.

### Reminder

- Inside and outside reminder window.
- Already reminded, cancelled, past, and malformed booking.
- With and without rice, special menu, extras, and action links.
- Duplicate scheduler run and concurrent workers.
- Evolution accepted, rejected, or timed out.
- Button rendering, URL correctness, and update `reminder_sent` only after accept.

### Cancellation And Deletion

- Without confirmation and with explicit confirmation.
- One booking, multiple bookings, and ambiguous selection.
- Missing, malformed, foreign, past, already cancelled, and already deleted ID.
- Archive and active-delete success or either operation failing.
- Staff notification failure.
- Duplicate and concurrent cancellation.
- Customer link, bot, and admin deletion paths.
- Modification-history and outbox reference cleanup.

## Not Run Or Constrained

| Scenario | Reason |
|---|---|
| Handset-generated inbound event | Signed Evolution-format payload injection used; operator handset needed |
| Voice, video, and document download | Product intentionally sends text-only fallback |
| Call rejection | RC2 exposes no call-reject endpoint; fallback only |
| Bulk advertising campaign | Would message real customer list |
| Full reminder scheduler | Would message all due real bookings; one isolated reminder tested |
| MySQL, Redis, or Evolution outage injection | Would disrupt production |
| Concurrent create/modify race | Duplicate webhook dedupe tested safely; booking race risks real duplicates |
| Durable outbox retry | `BookingConfirmationOutbox.Enabled=false`; table not active |

## Logs Reviewed

- `/var/log/bot.log`
- `botgenerator-evolution-staging-api` container logs
- Evolution PostgreSQL `evolution_api.Message`
- Production MySQL `bookings`, `cancelled_bookings`, and `modification_history`

Expected negative-case entries:

- `Booking 2750 not found` after repeated cancellation.
- `Booking 999999 not found` during invalid modification.

No unexpected C# exception, reconnect conflict, webhook authentication bypass,
or QA database residue remained after cleanup. Runtime Redis URI was scrubbed
from canonical bot log after testing.
