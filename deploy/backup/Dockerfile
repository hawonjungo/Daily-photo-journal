FROM alpine:3
RUN apk add --no-cache postgresql18-client rclone && adduser -D backup
COPY backup.sh run.sh restore.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/run.sh /usr/local/bin/restore.sh
USER backup
# The remote is defined by RCLONE_CONFIG_R2_* variables, so no config file is needed.
ENV RCLONE_CONFIG=/dev/null
# Healthy only if a backup succeeded in the last 25 hours, so `docker compose ps` shows when they stop working.
HEALTHCHECK --interval=5m --timeout=5s --start-period=10m \
  CMD test -n "$(find /tmp/last-backup-success -mmin -1500 2>/dev/null)"
ENTRYPOINT ["/usr/local/bin/run.sh"]
