#!/usr/bin/env bash
set -euo pipefail
umask 077

readonly stack_dir=/opt/botgenerator-evolution-staging
readonly backup_root="${BACKUP_ROOT:-/var/backups/botgenerator-evolution-staging}"
readonly timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
readonly compose=(docker compose --project-name botgenerator-evolution-staging --env-file "$stack_dir/.env" --file "$stack_dir/compose.yaml")

if (( EUID != 0 )); then
    printf 'Run this backup with sudo.\n' >&2
    exit 1
fi

if [[ ! -f "$stack_dir/.env" || ! -f "$stack_dir/compose.yaml" ]]; then
    printf 'Staging stack configuration is missing from %s.\n' "$stack_dir" >&2
    exit 1
fi

install -d -o root -g root -m 0700 "$backup_root"
backup_temp="$(mktemp -d "$backup_root/.backup-${timestamp}.XXXXXX")"
backup_dir="$backup_root/$timestamp"
trap 'rm -rf "$backup_temp"' EXIT

"${compose[@]}" exec -T postgres sh -ec \
    'exec pg_dump --format=custom --no-owner --no-privileges --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' \
    > "$backup_temp/postgresql.dump"

docker run --rm --network none --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m \
    --volume botgenerator_evolution_staging_instances:/instances:ro \
    --volume "$backup_temp:/backup:rw" \
    alpine:3.20 \
    tar --create --gzip --file /backup/instances.tar.gz --directory /instances .

(cd "$backup_temp" && sha256sum postgresql.dump instances.tar.gz > SHA256SUMS)
chmod 0600 "$backup_temp/postgresql.dump" "$backup_temp/instances.tar.gz" "$backup_temp/SHA256SUMS"
chmod 0700 "$backup_temp"
mv "$backup_temp" "$backup_dir"
trap - EXIT

printf 'Backup completed: %s\n' "$backup_dir"
