# WhatsApp ACK and Schedule Fix Verification

Date: 2026-07-31

## Versions

| Component | Version |
|---|---|
| Evolution source | `3f14e21b95fc31aac70989b14099923e150db4b4` |
| Evolution image | `botgenerator/evolution-api:2.4.0-rc2-baileys-rc14-ackfix1` |
| Evolution image ID | `sha256:c4f5c5c1a19b9786d432dfc4ed166253c5941106d9bdee8f20b61a38fbe9d6a0` |
| BotGenerator source | `dec5e8c4802385ec7899c843b86006e458814513` |

## Backups

- Evolution PostgreSQL and instance auth:
  `/var/backups/botgenerator-evolution-staging/20260731T151904Z`
- Previous BotGenerator publish directory:
  `/var/backups/botgenerator-service/publish-dir-20260731T152347Z`

## Automated Verification

| Check | Result |
|---|---|
| Evolution focused ACK tests | PASS, 4/4 |
| Evolution TypeScript build | PASS |
| BotGenerator core tests | PASS, 180/180 |
| BotGenerator integration tests | PASS, 6/6 |
| BotGenerator total | PASS, 186/186 |
| Git whitespace checks | PASS |

Full Evolution lint remains blocked by a pre-existing formatting finding in
`whatsapp.baileys.service.ts:246`; modified files pass focused formatting checks.

## ACK Live Verification

Instance `botgenerator-staging` reconnected as `open` after API-only recreation.
PostgreSQL and Redis containers were not recreated.

Initial mixed contract:

| Contract | Final primary status | MessageUpdate rows |
|---|---|---:|
| Text | `DELIVERY_ACK` | 1 |
| Reply button | `DELIVERY_ACK` | 1 |
| URL button | `DELIVERY_ACK` | 1 |
| List | `DELIVERY_ACK` | 1 |
| Contact | `DELIVERY_ACK` | 1 |

Additional canary sent ten text and five reply-button messages. All 15 primary
rows reached `READ`. Update history contained exactly:

```text
DELIVERY_ACK | 15
READ         | 15
```

Total live messages verified after deployment: 20. No primary row remained
`PENDING`, and no regressive update appeared.

## Schedule Live Verification

| Scenario | Tool path | Result |
|---|---|---|
| General weekly policy | `get_restaurant_info` | Open Thursday-Sunday; closed Monday-Wednesday |
| Default-open Friday `04/09/2026` | `check_day_capacity` | Open |
| Default-closed Monday `07/09/2026` | `check_day_capacity` | Closed |
| Explicit-closed Thursday `03/09/2026` | `check_day_capacity` | Closed |
| Temporary explicit-open Monday `07/09/2026` | `check_day_capacity` | Open |

Temporary explicit-open fixture was deleted after assertion. Remaining fixture
row count: `0`.

## Runtime Health

- Evolution API container: healthy
- Evolution PostgreSQL container: healthy
- Evolution Redis container: healthy
- Evolution business endpoint: HTTP `200`
- WhatsApp instance state: `open`
- `botgenerator.service`: active
- BotGenerator health endpoint: healthy

## Rollback

Evolution rollback image:

```text
botgenerator/evolution-api:2.4.0-rc2-baileys-rc14-pairing-debug
```

BotGenerator rollback source is preserved in previous publish backup listed above.
