-- =============================================================================
-- WaterDrop — Manual seed: drinking water facilities in Hamburg
-- =============================================================================
-- Purpose
--   The ToiletData table currently only contains records tagged
--   amenity="toilets". As a result the "Drinking Water" filter shows 0 results.
--   This script inserts a small set of plausible drinking water fountain
--   locations for Hamburg so the feature is visible in the UI.
--
-- How to run
--   Open https://portal.azure.com → SQL databases → waterdrop-database →
--   Query editor (preview). Sign in with WaterAdmin. Paste the contents of
--   this file and click Run.
--
-- Idempotent
--   Each INSERT is guarded with `IF NOT EXISTS`, so running this script twice
--   does NOT duplicate rows. Safe to re-run.
--
-- IMPORTANT — coordinates are approximate landmark positions
--   These are plausible locations in well-known Hamburg public spaces. They
--   are NOT verified against real drinking-fountain coordinates from the City
--   of Hamburg or OpenStreetMap. For a production deployment, replace with
--   verified data via the Overpass importer (planned follow-up task).
--
-- ElementId numbering
--   We use the range 9000000001 .. 9000000010 for these seed rows. Real OSM
--   element IDs are also numeric, so this range was chosen to be high enough
--   to avoid collision with any current or near-future OSM ID seen in the
--   wild. The UNIQUE index on ElementId guarantees no duplicates anyway.
-- =============================================================================

SET NOCOUNT ON;

-- Each block: only insert if no row with this ElementId already exists.

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000001)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000001, 53.5921, 10.0181, 'node', 'Hamburg', 'Stadtpark — Nordseite',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Stadtpark Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000002)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000002, 53.5587, 9.9846, 'node', 'Hamburg', 'Planten un Blomen',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Planten un Blomen Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000003)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000003, 53.5664, 9.9885, 'node', 'Hamburg', 'Alstervorland',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Alstervorland Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000004)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000004, 53.5651, 9.9622, 'node', 'Hamburg', 'Schanzenpark',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Schanzenpark Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000005)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000005, 53.5414, 9.9989, 'node', 'Hamburg', 'HafenCity Magellan-Terrassen',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"HafenCity Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000006)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000006, 53.5503, 9.9923, 'node', 'Hamburg', 'Rathausmarkt',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Rathausmarkt Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000007)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000007, 53.5528, 10.0067, 'node', 'Hamburg', 'Hauptbahnhof Vorplatz',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Hauptbahnhof Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000008)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000008, 53.5535, 9.9920, 'node', 'Hamburg', 'Jungfernstieg',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Jungfernstieg Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000009)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000009, 53.5712, 9.9094, 'node', 'Hamburg', 'Altonaer Volkspark',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Altonaer Volkspark Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Toilets WHERE ElementId = 9000000010)
INSERT INTO dbo.Toilets (Id, ElementId, Lat, Lon, Type, City, Name,
                         IsAccessible, HasChangingTable, HasFee,
                         AccessType, OpeningHours, TagsJson,
                         ImportedAt, LastUpdated, UserComment, UserPictureUrl)
VALUES (NEWID(), 9000000010, 53.5630, 9.9920, 'node', 'Hamburg', 'Binnenalster — Westufer',
        NULL, NULL, 0, 'public', '24/7',
        N'{"amenity":"drinking_water","name":"Binnenalster Trinkbrunnen","fee":"no"}',
        SYSUTCDATETIME(), NULL, NULL, NULL);

-- Quick sanity check after running: should report at least 10 drinking water rows.
SELECT COUNT(*) AS DrinkingWaterCount
FROM dbo.Toilets
WHERE TagsJson LIKE '%"amenity":"drinking_water"%';
