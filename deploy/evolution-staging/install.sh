#!/usr/bin/env bash
set -euo pipefail
umask 077

readonly target_dir=/opt/botgenerator-evolution-staging
readonly source_dir="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly compose_file="$target_dir/compose.yaml"
readonly env_file="$target_dir/.env"

if (( EUID != 0 )); then
    printf 'Run this installer with sudo.\n' >&2
    exit 1
fi

for required_file in compose.yaml scripts/backup.sh scripts/contract-test.sh scripts/health.sh; do
    if [[ ! -f "$source_dir/$required_file" ]]; then
        printf 'Required source file is missing: %s\n' "$required_file" >&2
        exit 1
    fi
done

install -d -o root -g root -m 0750 "$target_dir"
install -d -o root -g root -m 0750 "$target_dir/scripts"
install -m 0644 -o root -g root "$source_dir/compose.yaml" "$compose_file"
install -m 0750 -o root -g root "$source_dir/scripts/backup.sh" "$target_dir/scripts/backup.sh"
install -m 0750 -o root -g root "$source_dir/scripts/contract-test.sh" "$target_dir/scripts/contract-test.sh"
install -m 0750 -o root -g root "$source_dir/scripts/health.sh" "$target_dir/scripts/health.sh"

if [[ ! -e "$env_file" ]]; then
    postgres_password="$(openssl rand -hex 32)"
    api_key="$(openssl rand -hex 32)"
    env_temp="$(mktemp "$target_dir/.env.XXXXXX")"
    trap 'rm -f "$env_temp"' EXIT

    printf '%s\n' \
        'POSTGRES_DB=botgenerator_evolution_staging' \
        'POSTGRES_USER=botgenerator_evolution_staging' \
        "POSTGRES_PASSWORD=$postgres_password" \
        "AUTHENTICATION_API_KEY=$api_key" > "$env_temp"

    chown root:root "$env_temp"
    chmod 0600 "$env_temp"
    mv -f "$env_temp" "$env_file"
    trap - EXIT
else
    if [[ ! -f "$env_file" ]]; then
        printf 'Refusing non-file runtime environment path: %s\n' "$env_file" >&2
        exit 1
    fi
    chown root:root "$env_file"
    chmod 0600 "$env_file"
fi

compose=(docker compose --project-name botgenerator-evolution-staging --env-file "$env_file" --file "$compose_file")
"${compose[@]}" config -q
"${compose[@]}" pull
"${compose[@]}" up --detach --wait --wait-timeout 180
"$target_dir/scripts/health.sh"

printf 'BotGenerator Evolution staging stack is ready on 127.0.0.1:8108. Credentials were generated or preserved without display.\n'
