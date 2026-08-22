using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations.Drafts;

/// <summary>
/// Unregistered target-schema draft. It is never discovered or run automatically.
/// Required parent tables: Stations, Units, SecurityShiftProfiles.
/// </summary>
public sealed class EventTargetSchemaMigration : IDatabaseMigration
{
    public const string MigrationId = "event-target-schema-v1-draft";
    public const int DraftFromVersion = 2;
    public const int DraftToVersion = 3;

    public const string Sql = """
        CREATE TABLE Events (
            EventId TEXT COLLATE BINARY PRIMARY KEY NOT NULL
                CHECK (length(EventId) = 26)
                CHECK (EventId NOT GLOB '*[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]*'),
            StationId TEXT COLLATE BINARY NOT NULL,
            UnitId TEXT COLLATE BINARY NOT NULL CHECK (length(trim(UnitId)) > 0),
            EventType TEXT COLLATE BINARY NOT NULL
                CHECK (EventType IN ('START', 'NSD', 'ESD', 'OH')),
            EventDate INTEGER NOT NULL
                CHECK (typeof(EventDate) = 'integer')
                CHECK (EventDate BETWEEN 10000101 AND 99991231)
                CHECK (((EventDate / 100) % 100) BETWEEN 1 AND 12)
                CHECK ((EventDate % 100) BETWEEN 1 AND 31),
            EventTime INTEGER NOT NULL
                CHECK (typeof(EventTime) = 'integer')
                CHECK (EventTime BETWEEN 0 AND 1439),
            EventDateTime INTEGER NOT NULL
                CHECK (typeof(EventDateTime) = 'integer')
                CHECK (((EventDateTime % 1440) + 1440) % 1440 = EventTime),
            Remark TEXT NULL,
            CreatedAt TEXT NOT NULL CHECK (length(CreatedAt) >= 20 AND substr(CreatedAt, -1) = 'Z'),
            CreatedByShiftProfileId TEXT COLLATE BINARY NOT NULL,
            UpdatedAt TEXT NULL
                CHECK (UpdatedAt IS NULL OR (length(UpdatedAt) >= 20 AND substr(UpdatedAt, -1) = 'Z')),
            IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
            DeletedAt TEXT NULL
                CHECK (DeletedAt IS NULL OR (length(DeletedAt) >= 20 AND substr(DeletedAt, -1) = 'Z')),
            DeletedByShiftProfileId TEXT COLLATE BINARY NULL,
            RowVersion INTEGER NOT NULL DEFAULT 1 CHECK (RowVersion > 0),
            CONSTRAINT FK_Events_Station FOREIGN KEY (StationId)
                REFERENCES Stations (StationId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT FK_Events_Unit FOREIGN KEY (StationId, UnitId)
                REFERENCES Units (StationId, UnitId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT FK_Events_CreatedBy FOREIGN KEY (CreatedByShiftProfileId)
                REFERENCES SecurityShiftProfiles (ShiftProfileId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT FK_Events_DeletedBy FOREIGN KEY (DeletedByShiftProfileId)
                REFERENCES SecurityShiftProfiles (ShiftProfileId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT CK_Events_UpdatedAt CHECK (UpdatedAt IS NULL OR UpdatedAt >= CreatedAt),
            CONSTRAINT CK_Events_DeletionState CHECK (
                (IsDeleted = 0 AND DeletedAt IS NULL AND DeletedByShiftProfileId IS NULL)
                OR
                (IsDeleted = 1 AND DeletedAt IS NOT NULL AND DeletedByShiftProfileId IS NOT NULL
                    AND DeletedAt >= CreatedAt)
            )
        );

        CREATE UNIQUE INDEX UX_Events_ActiveUnitTimestamp
            ON Events (StationId, UnitId, EventDateTime)
            WHERE IsDeleted = 0;
        CREATE INDEX IX_Events_StationDateTime
            ON Events (StationId, EventDateTime, UnitId, EventId);
        CREATE INDEX IX_Events_UnitChain
            ON Events (StationId, UnitId, IsDeleted, EventDateTime, EventId);
        CREATE INDEX IX_Events_PersianDate
            ON Events (StationId, EventDate, IsDeleted, UnitId, EventTime);

        CREATE TABLE EventAudit (
            AuditId TEXT COLLATE BINARY PRIMARY KEY NOT NULL
                CHECK (length(AuditId) = 26)
                CHECK (AuditId NOT GLOB '*[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]*'),
            EventId TEXT COLLATE BINARY NOT NULL,
            ActionType TEXT COLLATE BINARY NOT NULL
                CHECK (ActionType IN ('ADD', 'EDIT', 'DELETE')),
            OldValue TEXT NULL,
            NewValue TEXT NULL,
            ActorShiftProfileId TEXT COLLATE BINARY NOT NULL,
            PersonnelNoSnapshot TEXT NULL,
            SupervisorDisplayNameSnapshot TEXT NULL,
            TimestampUtc TEXT NOT NULL
                CHECK (length(TimestampUtc) >= 20 AND substr(TimestampUtc, -1) = 'Z'),
            Reason TEXT NOT NULL CHECK (length(trim(Reason)) > 0),
            CorrelationId TEXT NOT NULL CHECK (length(trim(CorrelationId)) > 0),
            CONSTRAINT FK_EventAudit_Event FOREIGN KEY (EventId)
                REFERENCES Events (EventId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT FK_EventAudit_Actor FOREIGN KEY (ActorShiftProfileId)
                REFERENCES SecurityShiftProfiles (ShiftProfileId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT CK_EventAudit_ActionShape CHECK (
                (ActionType = 'ADD' AND OldValue IS NULL AND NewValue IS NOT NULL)
                OR
                (ActionType = 'EDIT' AND OldValue IS NOT NULL AND NewValue IS NOT NULL)
                OR
                (ActionType = 'DELETE' AND OldValue IS NOT NULL AND NewValue IS NULL)
            )
        );

        CREATE INDEX IX_EventAudit_EventTimeline
            ON EventAudit (EventId, TimestampUtc, AuditId);
        CREATE INDEX IX_EventAudit_ActorTimeline
            ON EventAudit (ActorShiftProfileId, TimestampUtc, AuditId);

        CREATE TRIGGER TR_EventAudit_NoUpdate
        BEFORE UPDATE ON EventAudit
        BEGIN
            SELECT RAISE(ABORT, 'EventAudit is append-only');
        END;

        CREATE TRIGGER TR_EventAudit_NoDelete
        BEFORE DELETE ON EventAudit
        BEGIN
            SELECT RAISE(ABORT, 'EventAudit is append-only');
        END;

        CREATE TRIGGER TR_Events_ImmutableFields
        BEFORE UPDATE ON Events
        WHEN NEW.EventId <> OLD.EventId
          OR NEW.StationId <> OLD.StationId
          OR NEW.CreatedAt <> OLD.CreatedAt
          OR NEW.CreatedByShiftProfileId <> OLD.CreatedByShiftProfileId
        BEGIN
            SELECT RAISE(ABORT, 'Immutable Event fields cannot be changed');
        END;

        CREATE TRIGGER TR_Events_NoMutationAfterDelete
        BEFORE UPDATE ON Events
        WHEN OLD.IsDeleted = 1
        BEGIN
            SELECT RAISE(ABORT, 'Deleted Events cannot be mutated');
        END;
        """;

    public EventTargetSchemaMigration(IChecksumService checksumService)
    {
        ArgumentNullException.ThrowIfNull(checksumService);
        Metadata = new MigrationMetadata(
            MigrationId,
            DraftFromVersion,
            DraftToVersion,
            "Create isolated target Events and EventAudit schema.",
            checksumService.Compute(Sql));
    }

    public MigrationMetadata Metadata { get; }
    public string ChecksumPayload => Sql;

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
