# Relifes – Daily Photo Journal

Personal daily photo journal: snap a photo of food, add the price, attach the place and time, then browse history and summaries.

- **API** (`api/`): ASP.NET Core, EF Core, PostgreSQL. Images are stored in Cloudflare R2.
- **Web** (`web/`): installable React PWA with Timeline, Summary and Settings pages.
- **Phone**: photos are sent with [HTTP Shortcuts](docs/http-shortcuts.md) on Android.
- **Deploy** (`deploy/`): Docker Compose with Caddy, and daily database backups to R2.

Viewing or editing a single entry (`GET/PUT/DELETE /api/entries/{id}`) is not built yet.

## Requirements
- .NET SDK 10
- Node.js 22
- Docker with Compose (the API tests also use it, to start their own Postgres)
- EF Core tools: `dotnet tool install --global dotnet-ef`
- A private Cloudflare R2 bucket and an API token with read and write access to it

## Local setup
Run these from the repo root.

1. Create `deploy/.env` and set a password:
   ```sh
   cp deploy/.env.example deploy/.env
   ```
2. Start Postgres (reachable only from this machine, on `127.0.0.1:5432`):
   ```sh
   docker compose -f deploy/docker-compose.yml up -d --wait
   ```
3. Give the API its secrets, using the same database password:
   ```sh
   dotnet user-secrets --project api/Relifes.Api set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=relifes;Username=relifes;Password=<password>"
   dotnet user-secrets --project api/Relifes.Api set "R2:ServiceUrl" "https://<account id>.r2.cloudflarestorage.com"
   dotnet user-secrets --project api/Relifes.Api set "R2:AccessKeyId" "<access key id>"
   dotnet user-secrets --project api/Relifes.Api set "R2:SecretAccessKey" "<secret access key>"
   dotnet user-secrets --project api/Relifes.Api set "R2:BucketName" "<bucket name>"
   dotnet user-secrets --project api/Relifes.Api set "Places:GoogleApiKey" "<optional Google Places key>"
   ```
4. Apply migrations:
   ```sh
   dotnet ef database update --project api/Relifes.Api
   ```
5. Create your user. This prints the API key once, and only its hash is stored:
   ```sh
   dotnet run --project api/Relifes.Api --launch-profile http -- create-user you@example.com "Your Name"
   ```
6. Run the API. It refuses to start if any `R2` setting is missing:
   ```sh
   dotnet run --project api/Relifes.Api --launch-profile http
   ```
   Check it at <http://localhost:5137/health>. It returns `Healthy` when the database is reachable.
7. Run the web app, then open <http://localhost:5173> and enter the API key in Settings:
   ```sh
   cd web
   npm install
   npm run dev
   ```

Migrations are not applied on startup. After changing an entity:
```sh
dotnet ef migrations add <Name> --project api/Relifes.Api --output-dir Data/Migrations
dotnet ef database update --project api/Relifes.Api
```

## Tests
```sh
dotnet test api/Relifes.slnx
cd web && npm test
```
Docker must be running for the API tests. They use a temporary Postgres container and fakes for R2 and Google, so they need no accounts and cost nothing.

## API
Every endpoint except `/health` needs the `X-Api-Key` header. Dates are the local date of each entry, as `yyyy-MM-dd`.

| Endpoint | Purpose |
|---|---|
| `POST /api/entries` | Create an entry from a photo |
| `GET /api/entries?from=&to=` | Timeline grouped by local date with per-currency totals. Defaults to the last 30 days; at most 366 days |
| `GET /api/places/nearby?lat=&lng=` | Up to 10 places within about 80 m |
| `GET /api/summary?period=day\|week\|month&date=` | Spend per currency, count, top 5 places and categories. Weeks run Monday to Sunday |

### Creating an entry
`POST /api/entries` takes `multipart/form-data`:

```sh
curl -X POST http://localhost:5137/api/entries \
  -H "X-Api-Key: <api key>" \
  -F "image=@photo.jpg" \
  -F "price=45000" -F "currency=VND" \
  -F "lat=10.7769" -F "lng=106.7009" \
  -F "placeName=Bun Bo" \
  -F "category=Food" \
  -F "takenAt=2026-11-01T12:30:00+07:00" \
  -F "timeZone=Asia/Ho_Chi_Minh"
```

| Field | Required | Format |
|---|---|---|
| `image` | yes | JPEG, PNG or WebP, up to 20 MB and 64 megapixels |
| `price` | no | Digits with an optional `.` and at most 2 decimals. `12,5` and `50.000` are rejected, not guessed at |
| `currency` | with `price` | 3-letter ISO code |
| `lat`, `lng` | no | Decimal degrees, sent together |
| `placeId` or `placeName` | no | One or the other. A `placeName` reuses your existing place with that name, ignoring case |
| `category` | no | Free text, up to 50 characters |
| `takenAt` | no | ISO 8601 with an offset or `Z`. Defaults to now |
| `timeZone` | no | IANA name. Defaults to `Entries:DefaultTimeZone` |

Empty fields count as missing. It returns `201` with the entry, `400` for invalid fields, `401` for a missing or wrong key, `413` for a file that is too large, and `502` if R2 fails. Nothing is saved in the last case.

Images are stored as WebP (quality 80, longest side at most 1600 px, thumbnail 400 px), rotated upright, converted to sRGB and with all metadata removed. HEIC is not supported yet. The bucket stays private: the timeline returns signed links that expire after 6 hours.

### Nearby places
The API checks its own places first and calls Google only if none is within about 80 m. Results from Google are saved for next time, and an empty result is remembered for 24 hours.

The response has `places`, a `source` (`database`, `google` or `none`) and, when empty, a `fallbackReason`: `no_results`, `daily_limit_reached`, `google_error` or `not_configured`. In all of these cases the client should ask for a manual `placeName`.

Calls are limited to `Places:DailyGoogleCap` per UTC day. The counter is stored in the database, so it survives restarts and holds under concurrent requests. Failed calls also count.

## Configuration
| Setting | Environment variable | Used for |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | Postgres connection for the API |
| `R2:ServiceUrl` | `R2__ServiceUrl` | R2 endpoint. `http` works only with a local S3 server |
| `R2:AccessKeyId` | `R2__AccessKeyId` | R2 access key id |
| `R2:SecretAccessKey` | `R2__SecretAccessKey` | R2 secret access key |
| `R2:BucketName` | `R2__BucketName` | R2 bucket |
| `Places:GoogleApiKey` | `Places__GoogleApiKey` | Google Places key. Without it Google is never called |
| `Places:DailyGoogleCap` | `Places__DailyGoogleCap` | Google calls allowed per day. Default 150 |
| `Places:SearchRadiusMeters` | `Places__SearchRadiusMeters` | Search radius. Default 80 |
| `Entries:DefaultTimeZone` | `Entries__DefaultTimeZone` | Time zone for uploads that send no `timeZone`. Default `UTC` |

In production these are set from `deploy/.env`, listed in [deploy/.env.example](deploy/.env.example). Never commit secrets: `deploy/.env` is ignored by git.

## Deploy
`deploy/docker-compose.prod.yml` runs `caddy`, `api`, `web`, `db` and `backup` on an Oracle Cloud VM. Caddy gets the HTTPS certificate by itself.

### 1. Prepare the VM
1. Install Docker with the Compose plugin.
2. In Oracle Cloud, add ingress rules to the VCN security list for TCP 80, TCP 443 and UDP 443.
3. Open the same ports in the VM's own firewall. Oracle's Ubuntu images reject them by default:
   ```sh
   sudo iptables -I INPUT 5 -p tcp --dport 80 -j ACCEPT
   sudo iptables -I INPUT 5 -p tcp --dport 443 -j ACCEPT
   sudo iptables -I INPUT 5 -p udp --dport 443 -j ACCEPT
   sudo netfilter-persistent save
   ```
4. In Cloudflare DNS, add an `A` record for `photo` pointing to the VM's public IP, set to **DNS only** (grey cloud), so that Caddy can get its certificate directly.

### 2. Configure
On the VM, copy the repo, then:
```sh
cp deploy/.env.example deploy/.env
openssl rand -hex 24   # use this as POSTGRES_PASSWORD
nano deploy/.env
```

### 3. Start
```sh
COMPOSE="docker compose -f deploy/docker-compose.prod.yml"
$COMPOSE build
$COMPOSE up -d db
$COMPOSE run --rm api migrate
$COMPOSE up -d
$COMPOSE run --rm api create-user you@example.com "Your Name"   # prints the API key once
```
On an empty database, the first `migrate` logs one `fail:` line about `__EFMigrationsHistory`. That is EF Core checking for its history table before creating it, and is expected.

Check <https://photo.relifes.net/health>, then open the site and enter the API key in Settings. To set up the phone, follow [docs/http-shortcuts.md](docs/http-shortcuts.md).

### Updating
```sh
git pull
$COMPOSE build
$COMPOSE run --rm api migrate
$COMPOSE up -d
```

### Backups
The `backup` service dumps the database when it starts and then every 24 hours, uploads the dump to R2 and deletes dumps older than 30 days. A failed backup is retried after an hour, and the service shows as `unhealthy` in `$COMPOSE ps` if there has been no successful backup for 25 hours. Check on this from time to time, because nothing sends an alert.

Images already live in R2, so only the database needs backing up. By default dumps go to the same bucket under `backups/`, so a problem with that bucket affects both: set `BACKUP_BUCKET` to keep them apart.

To restore:
```sh
$COMPOSE exec backup restore.sh                # list backups
$COMPOSE stop api
$COMPOSE exec db psql -U relifes -d postgres -c 'DROP DATABASE relifes' -c 'CREATE DATABASE relifes'
$COMPOSE exec backup restore.sh <backup file> relifes
$COMPOSE start api
```
This replaces all current data. To check a backup without touching anything, restore it into a new database with a different name instead.
