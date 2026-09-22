# Resource Booking System

A hackathon resource-booking system: ASP.NET Core Web API + PostgreSQL backend,
React/Vite frontend (added separately — see the follow-up frontend delivery).

> **Status: backend code complete, not yet verified.** This was written without
> a local .NET SDK / PostgreSQL instance available to the tool that generated
> it. Follow the steps below and treat any error you hit as expected — work
> through it with the troubleshooting notes, or bring the exact error back for
> a fix. Do not assume anything here compiles or runs correctly until you've
> actually run it.

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+ running locally (or via Docker)
- Node.js 18+ (for the frontend, added separately)

## 1. Restore and configure secrets

Secrets are **not** committed — `appsettings.json` only has placeholders.
From `BookingApi/`:

```bash
cd BookingApi
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=bookingdb;Username=postgres;Password=YOUR_REAL_PASSWORD"
dotnet user-secrets set "Resend:ApiKey" "YOUR_RESEND_API_KEY"
```

(`Jwt:Secret` must be at least 32 characters — the app throws on startup if it
isn't set, rather than silently running with a weak default.)

## 2. Create the database and run migrations

```bash
createdb bookingdb   # or: psql -U postgres -c "CREATE DATABASE bookingdb;"

dotnet ef migrations add InitialCreate
dotnet ef database update
```

If `dotnet ef` isn't found: `dotnet tool install --global dotnet-ef`.

Then apply the exclusion constraint that actually enforces no-overlapping-bookings
at the database level (this is **not** created by EF Core migrations, since
`EXCLUDE USING gist` isn't something EF Core's model builder can express):

```bash
psql -U postgres -d bookingdb -f Data/Sql/001_booking_exclusion_constraint.sql
```

## 3. Run

```bash
dotnet build
dotnet run
```

Visit `https://localhost:{port}/swagger` (port shown in the console output) to
confirm it's actually up, and hit `GET /api/health` first — that's the
simplest possible proof the app started and the pipeline is wired correctly.

## 4. Verify the core guarantee (Part 7 / Part 25)

Register a user, log in, create a resource as an admin (you'll need to
manually promote your user to the ADMIN role in the database for the first
admin — there's no bootstrap endpoint by design, since exposing one would be
a privilege-escalation risk):

```sql
UPDATE "AspNetUsers" SET "Id" = "Id"; -- no-op, just to locate your user id
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'you@example.com' AND r."Name" = 'ADMIN';
```

Then send two genuinely concurrent `POST /api/bookings` requests for the same
resource and time window (e.g. with `xargs -P2` running two `curl` calls, or a
small script firing both without awaiting one before the other). Expected:

- One returns `201 Created`
- The other returns `409 Conflict` with `code: "BOOKING_CONFLICT"`
- `SELECT COUNT(*) FROM "Bookings" WHERE "ResourceId" = '...' AND "Status" = 0`
  for that window returns exactly `1`

If both succeed, the exclusion constraint from step 2 was not actually applied
— check `\d "Bookings"` in `psql` for a constraint named
`no_overlapping_confirmed_bookings`.

## What's genuinely implemented vs. what still needs your verification

Implemented as real code (Controllers/Services/DTOs/Models all present):
auth + JWT + roles, resources CRUD, bookings CRUD, the exclusion-constraint
double-booking guard, optimistic concurrency via `xmin`, SignalR real-time
updates, recurring bookings, QR check-in, waiting list with next-in-line
notification, Resend email (fire-and-forget, never blocks booking), a
reminder background service, and the admin dashboard/bookings/users endpoints.

**Not yet verified — do this yourself and report back any error:**
- `dotnet build` succeeds with no errors
- `dotnet ef migrations add InitialCreate` produces a sane migration
- The exclusion constraint SQL actually applies without error
- Package versions in `BookingApi.csproj` resolve — pin/adjust if NuGet has
  moved past what's listed
- The full concurrent-booking test in step 4 above actually behaves as
  described
