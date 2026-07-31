#!/usr/bin/env bash
set -euo pipefail

readonly stack_dir=/opt/botgenerator-evolution-staging
readonly compose=(docker compose --project-name botgenerator-evolution-staging --env-file "$stack_dir/.env" --file "$stack_dir/compose.yaml")
readonly services=(api postgres redis)

if (( EUID != 0 )); then
    printf 'Run this health check with sudo.\n' >&2
    exit 1
fi

if [[ ! -f "$stack_dir/.env" || ! -f "$stack_dir/compose.yaml" ]]; then
    printf 'Staging stack configuration is missing from %s.\n' "$stack_dir" >&2
    exit 1
fi

for service in "${services[@]}"; do
    container_id="$("${compose[@]}" ps --quiet "$service")"
    if [[ -z "$container_id" ]]; then
        printf '%s container is not running.\n' "$service" >&2
        exit 1
    fi

    health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id")"
    if [[ "$health" != healthy ]]; then
        printf '%s container health is %s.\n' "$service" "$health" >&2
        exit 1
    fi
done

"${compose[@]}" exec -T postgres sh -ec \
    'pg_isready --quiet --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"'
"${compose[@]}" exec -T redis redis-cli ping | grep --quiet '^PONG$'
curl --fail --silent --show-error --max-time 10 --header 'Origin: http://127.0.0.1:8108' --output /dev/null http://127.0.0.1:8108/

business_status="$("${compose[@]}" exec -T api node -e '
const http = require("http");
const request = http.get({
  hostname: "127.0.0.1",
  port: 8080,
  path: "/instance/fetchInstances",
  headers: {
    apikey: process.env.AUTHENTICATION_API_KEY,
    Origin: "http://127.0.0.1:8108",
  },
}, response => {
  response.resume();
  process.stdout.write(String(response.statusCode));
});
request.setTimeout(10000, () => request.destroy());
request.on("error", () => process.exit(1));
')"

if [[ "$business_status" != 200 ]]; then
    printf 'Evolution business endpoint GET /instance/fetchInstances returned HTTP %s.\n' "$business_status" >&2
    exit 1
fi

printf 'BotGenerator Evolution staging health: ok\n'
