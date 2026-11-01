#!/bin/sh
# Backs up on start and then once a day. A failed backup is retried after an hour.
while true; do
  if backup.sh; then
    touch /tmp/last-backup-success
    sleep 86400
  else
    echo "$(date -u +%FT%TZ) Backup failed, retrying in 1 hour" >&2
    sleep 3600
  fi
done
