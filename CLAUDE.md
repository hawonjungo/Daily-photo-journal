# Relifes – Daily Photo Journal

Personal daily photo journal. Main use case: snap a photo of food, add the price, auto-attach the place name + time, then browse history and see daily / weekly / monthly summaries.
Single user for now, but designed for ~10 users later.

## Stack
- **Backend:** ASP.NET Core Web API (.NET 10), EF Core, PostgreSQL
- **Frontend:** React + Vite + TypeScript, Tailwind CSS + shadcn/ui, installable PWA, mobile-first
- **Image storage:** Cloudflare R2 (S3-compatible, use AWSSDK.S3). Never store images on the VM disk.
- **Places lookup:** Google Places API (New) – Nearby Search
- **Deploy:** Docker Compose on an Oracle Cloud Always Free VM: `api`, `web`, `db`, `caddy` (reverse proxy + auto HTTPS)
- **Domain:** `photo.relifes.net` (DNS on Cloudflare)

## Repo layout
```
/api        ASP.NET Core Web API
/web        React app
/deploy     docker-compose.yml, Caddyfile, backup script
```

## Data model (every table has UserId from day one)
- **User**: Id, Email, DisplayName, ApiKeyHash, CreatedAt
- **Place**: Id, GooglePlaceId (nullable), Name, Address, Lat, Lng, CreatedByUserId
- **Entry**: Id, UserId, TakenAt (UTC) + TimeZone, ImageKey, ThumbKey, Price (decimal, nullable), Currency (ISO code, e.g. AUD, VND), PlaceId (nullable), Lat, Lng, Category, Note, Rating (1–5, nullable), CreatedAt, UpdatedAt

## API (v1)
- `POST /api/entries` – multipart: image, price, currency, lat, lng, placeId or placeName, takenAt. Auth via `X-Api-Key` header (used by Android HTTP Shortcuts).
- `GET /api/entries?from=&to=` – timeline
- `GET/PUT/DELETE /api/entries/{id}`
- `GET /api/places/nearby?lat=&lng=` – return up to 10 places. First check our own Place table within ~80 m; only call Google if nothing matches. Cache results.
- `GET /api/summary?period=day|week|month&date=` – total spend per currency, count, top places, by category

## Rules
- Resize on upload: full image max 1600px (WebP, ~80% quality), thumbnail 400px. Strip EXIF before storing.
- Google Places field mask: `places.id,places.displayName,places.formattedAddress,places.location` only (stay in Pro SKU). Never request rating/hours/photos.
- Hard daily cap on Google calls (config, default 150/day) – fall back to manual place name when exceeded.
- Money: decimal, never float. Never sum across different currencies.
- Store times in UTC + the user's time zone; group days by the user's local date.
- All secrets via environment variables / `.env` (never commit).
- Private by default: no public pages in v1.

## UI
- Mobile-first, clean, photo-forward. Light + dark mode.
- Pages: Timeline (grouped by day, daily total in header), Entry detail/edit, Summary (day/week/month tabs), Settings (API key, default currency).
- Use shadcn/ui components consistently; no custom one-off styles unless needed.

## Working style
- Small, focused changes. Explain the plan before large edits.
- Write tests for summary calculations and the nearby-places logic.
- Keep README updated with setup + deploy steps.
