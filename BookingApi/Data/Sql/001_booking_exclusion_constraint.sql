-- Run this AFTER `dotnet ef database update` has created the Bookings table.
--
-- Run it with: psql -U postgres -d bookingdb -f Data/Sql/001_booking_exclusion_constraint.sql
--
-- This is the actual, database-level guarantee against overlapping confirmed
-- bookings for the same resource (Part 7 of the spec). It works even under
-- genuinely concurrent transactions, unlike an application-level AnyAsync()
-- check, because Postgres enforces it as part of the row insert/update itself.

CREATE EXTENSION IF NOT EXISTS btree_gist;

-- BookingStatus enum is stored as an int by EF Core's default enum-to-int
-- conversion: Confirmed = 0, Cancelled = 1, Completed = 2, NoShow = 3.
-- We only need to prevent overlaps between rows that are still Confirmed —
-- a cancelled or completed booking should not block a new one.
ALTER TABLE "Bookings"
  ADD CONSTRAINT no_overlapping_confirmed_bookings
  EXCLUDE USING gist (
    "ResourceId" WITH =,
    tsrange("StartTime", "EndTime", '[)') WITH &&
  )
  WHERE ("Status" = 0);
