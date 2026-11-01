#!/bin/sh
# Lists backups, or restores one into the database named by the second argument.
#   restore.sh                          list available backups
#   restore.sh <backup file> <database> restore into an existing, empty database
set -eu

destination="r2:$BACKUP_BUCKET/$BACKUP_PREFIX"

if [ "$#" -eq 0 ]; then
  rclone lsl "$destination"
  exit 0
fi

if [ "$#" -ne 2 ]; then
  echo "Usage: restore.sh [<backup file> <database>]" >&2
  exit 1
fi

file="/tmp/restore-$1"
trap 'rm -f "$file"' EXIT

rclone copyto "$destination/$1" "$file"
pg_restore --exit-on-error --no-owner --dbname="$2" "$file"
echo "Restored $1 into $2"
