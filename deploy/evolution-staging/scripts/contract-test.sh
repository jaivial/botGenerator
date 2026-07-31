#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

readonly root_dir=/opt/botgenerator-evolution-staging
readonly env_file="$root_dir/.env"
readonly compose_file="$root_dir/compose.yaml"
readonly api_url=http://127.0.0.1:8108

usage() {
    printf '%s\n' \
        'Usage: sudo contract-test.sh --instance NAME --recipient E164 [--inbound-message-id ID]' \
        'Live sends never start unless both --instance and --recipient are explicitly supplied.' \
        'Approved staging recipient example: --recipient 34692747052'
}

instance=
recipient=
inbound_message_id=
while (( $# > 0 )); do
    case "$1" in
        --instance)
            instance="${2:-}"
            shift 2
            ;;
        --recipient)
            recipient="${2:-}"
            shift 2
            ;;
        --inbound-message-id)
            inbound_message_id="${2:-}"
            shift 2
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            usage >&2
            exit 2
            ;;
    esac
done

if (( EUID != 0 )); then
    printf 'Run with sudo so staging credentials and PostgreSQL are accessible.\n' >&2
    exit 1
fi
if [[ ! "$instance" =~ ^[A-Za-z0-9._-]+$ ]]; then
    printf 'Explicit --instance must contain only letters, numbers, dot, underscore, or hyphen.\n' >&2
    exit 2
fi
if [[ ! "$recipient" =~ ^[1-9][0-9]{6,14}$ ]]; then
    printf 'Explicit --recipient must be 7-15 E.164 digits without plus sign.\n' >&2
    exit 2
fi
if [[ -n "$inbound_message_id" && ! "$inbound_message_id" =~ ^[A-Za-z0-9._:-]{1,512}$ ]]; then
    printf 'Inbound message ID contains unsupported characters.\n' >&2
    exit 2
fi
if [[ ! -r "$env_file" || ! -r "$compose_file" ]]; then
    printf 'Installed staging configuration not found under %s.\n' "$root_dir" >&2
    exit 1
fi

# shellcheck disable=SC1090
. "$env_file"
: "${AUTHENTICATION_API_KEY:?Missing staging API key}"
: "${POSTGRES_USER:?Missing staging PostgreSQL user}"
: "${POSTGRES_DB:?Missing staging PostgreSQL database}"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT
curl_config="$work_dir/curl.conf"
response_file="$work_dir/response.json"
printf '%s\n' \
    'silent' \
    'show-error' \
    'connect-timeout = 5' \
    'max-time = 30' \
    'header = "Content-Type: application/json"' \
    'header = "Origin: http://127.0.0.1:8108"' \
    "header = \"apikey: $AUTHENTICATION_API_KEY\"" > "$curl_config"
unset AUTHENTICATION_API_KEY

message_ids=()

post_json() {
    local label=$1
    local route=$2
    local payload=$3
    local collect_id=${4:-false}
    local status
    status="$(curl --config "$curl_config" --request POST --data-binary @- \
        --output "$response_file" --write-out '%{http_code}' "$api_url$route" <<< "$payload")"
    if [[ ! "$status" =~ ^2[0-9][0-9]$ ]]; then
        printf '%s failed with HTTP %s. Response retained only in protected temporary storage.\n' "$label" "$status" >&2
        exit 1
    fi

    if [[ "$collect_id" == true ]]; then
        local message_id
        message_id="$(python3 - "$response_file" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as response:
    body = json.load(response)
message_id = body.get("key", {}).get("id") if isinstance(body, dict) else None
if not isinstance(message_id, str) or not message_id:
    raise SystemExit(1)
print(message_id)
PY
)" || {
            printf '%s returned no accepted message key.\n' "$label" >&2
            exit 1
        }
        if [[ ! "$message_id" =~ ^[A-Za-z0-9._:-]{1,512}$ ]]; then
            printf '%s returned unsafe message ID.\n' "$label" >&2
            exit 1
        fi
        message_ids+=("$message_id")
        printf '%s accepted: message ID %s\n' "$label" "$message_id"
    else
        printf '%s passed with HTTP %s.\n' "$label" "$status"
    fi
}

post_json 'number existence' "/chat/whatsappNumbers/$instance" \
    "{\"numbers\":[\"$recipient\"]}"
python3 - "$response_file" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as response:
    body = json.load(response)
items = body if isinstance(body, list) else body.get("response", body.get("data", []))
if isinstance(items, dict):
    items = [items]
if not isinstance(items, list) or not any(item.get("exists") is True for item in items if isinstance(item, dict)):
    raise SystemExit("Recipient is not registered on WhatsApp; no messages sent.")
PY

post_json 'text' "/message/sendText/$instance" \
    "{\"number\":\"$recipient\",\"text\":\"Evolution RC2 staging contract: text\"}" true
post_json 'reply buttons' "/message/sendButtons/$instance" \
    "{\"number\":\"$recipient\",\"title\":\"RC2 reply contract\",\"description\":\"Reply-only buttons\",\"footer\":\"Staging\",\"buttons\":[{\"type\":\"reply\",\"displayText\":\"OK\",\"id\":\"contract-ok\"}]}" true
post_json 'URL buttons' "/message/sendButtons/$instance" \
    "{\"number\":\"$recipient\",\"title\":\"RC2 URL contract\",\"description\":\"URL-only buttons\",\"footer\":\"Staging\",\"buttons\":[{\"type\":\"url\",\"displayText\":\"WEB\",\"url\":\"https://alqueriavillacarmen.com/\"}]}" true
post_json 'list' "/message/sendList/$instance" \
    "{\"number\":\"$recipient\",\"title\":\"RC2 list contract\",\"description\":\"Choose one\",\"footerText\":\"Staging\",\"buttonText\":\"Open\",\"sections\":[{\"title\":\"Options\",\"rows\":[{\"title\":\"Option one\",\"description\":\"Contract row\",\"rowId\":\"contract-row-1\"}]}]}" true
post_json 'contact' "/message/sendContact/$instance" \
    "{\"number\":\"$recipient\",\"contact\":[{\"fullName\":\"Evolution RC2 Staging\",\"wuid\":\"$recipient\",\"phoneNumber\":\"$recipient\",\"organization\":\"Alqueria Villa Carmen\"}]}" true

if [[ -n "$inbound_message_id" ]]; then
    post_json 'reaction add' "/message/sendReaction/$instance" \
        "{\"key\":{\"id\":\"$inbound_message_id\",\"remoteJid\":\"$recipient@s.whatsapp.net\",\"fromMe\":false},\"reaction\":\"\\ud83d\\udc40\"}"
    post_json 'reaction remove' "/message/sendReaction/$instance" \
        "{\"key\":{\"id\":\"$inbound_message_id\",\"remoteJid\":\"$recipient@s.whatsapp.net\",\"fromMe\":false},\"reaction\":\"\"}"
    post_json 'mark read' "/chat/markMessageAsRead/$instance" \
        "{\"readMessages\":[{\"id\":\"$inbound_message_id\",\"remoteJid\":\"$recipient@s.whatsapp.net\",\"fromMe\":false}]}"
    python3 - "$response_file" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as response:
    body = json.load(response)
if body.get("message") != "Read messages" or body.get("read") != "success":
    raise SystemExit("Mark-read did not return Evolution RC2 read success.")
PY
else
    printf 'Inbound phase skipped. Supply real --inbound-message-id to test reaction add/remove and mark-read.\n'
fi

sql_ids=
for message_id in "${message_ids[@]}"; do
    [[ -n "$sql_ids" ]] && sql_ids+=,
    sql_ids+="'$message_id'"
done
sql="SELECT m.key->>'id' AS message_id, COALESCE(m.status, 'NULL') AS status FROM evolution_api.\"Message\" m JOIN evolution_api.\"Instance\" i ON i.id = m.\"instanceId\" WHERE i.name = '$instance' AND m.key->>'id' IN ($sql_ids) ORDER BY m.\"messageTimestamp\", message_id;"

db_statuses=
for attempt in {1..10}; do
    db_statuses="$(printf '%s\n' "$sql" | docker compose --project-name botgenerator-evolution-staging \
        --env-file "$env_file" --file "$compose_file" exec -T postgres \
        sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --no-align --tuples-only --field-separator=" | "')"
    status_count="$(grep -c . <<< "$db_statuses" || true)"
    (( status_count == ${#message_ids[@]} )) && break
    sleep 1
done
if (( status_count != ${#message_ids[@]} )); then
    printf 'PostgreSQL contained %s of %s accepted message IDs.\n' "$status_count" "${#message_ids[@]}" >&2
    exit 1
fi
printf 'Evolution PostgreSQL statuses:\n%s\n' "$db_statuses"

printf 'Contract test completed. Calls and provider configuration were not modified.\n'
