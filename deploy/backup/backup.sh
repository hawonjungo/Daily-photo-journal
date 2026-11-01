#!/bin/sh
# Dumps the database and uploads it to R2. Connection settings come from the PG* and RCLONE_CONFIG_R2_* variables.
set -eu

stamp=$(date -u +%Y-%m-%dT%H%M%SZ)
name="relifes-$stamp.dump"
file="/tmp/$name"
destination="r2:$BACKUP_BUCKET/$BACKUP_PREFIX"
trap 'rm -f "$file"' EXIT

pg_dump --format=custom --file="$file"

# A dump that cannot be read back must not replace good backups.
pg_restore --list "$file" > /dev/null

rclone copyto "$file" "$destination/$name"

# Old backups are removed only here, after a new one is safely stored.
rclone delete --min-age "${BACKUP_RETENTION_DAYS}d" "$destination"

echo "$(date -u +%FT%TZ) Uploaded $destination/$name ($(wc -c < "$file") bytes)"
