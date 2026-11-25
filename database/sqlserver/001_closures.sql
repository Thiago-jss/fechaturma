SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Closures', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Closures (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Closures PRIMARY KEY,
        Organization int NOT NULL CHECK (Organization BETWEEN 1 AND 999),
        Term varchar(6) NOT NULL,
        ClassCode varchar(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
        Title nvarchar(120) NOT NULL,
        Fingerprint char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        Version int NOT NULL,
        StudentCount int NOT NULL CHECK (StudentCount BETWEEN 1 AND 200),
        ApprovedCount int NOT NULL,
        ImportedAt datetimeoffset(3) NOT NULL,
        ClosedAt datetimeoffset(3) NULL,
        CONSTRAINT CK_Closures_Counts CHECK (ApprovedCount BETWEEN 0 AND StudentCount),
        CONSTRAINT UQ_Closures_Class UNIQUE (Organization, Term, ClassCode),
        CONSTRAINT CK_Closures_State CHECK ((Version = 1 AND ClosedAt IS NULL) OR (Version = 2 AND ClosedAt IS NOT NULL))
    );
    CREATE INDEX IX_Closures_List ON dbo.Closures (Organization, ImportedAt DESC, Id DESC);
    CREATE TABLE dbo.ClosureRows (
        ClosureId uniqueidentifier NOT NULL REFERENCES dbo.Closures(Id),
        Registration varchar(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
        StudentName nvarchar(100) NOT NULL,
        Grade decimal(4,2) NOT NULL CHECK (Grade BETWEEN 0 AND 10),
        Attendance decimal(5,2) NOT NULL CHECK (Attendance BETWEEN 0 AND 100),
        Approved bit NOT NULL,
        CONSTRAINT PK_ClosureRows PRIMARY KEY (ClosureId, Registration)
    );
    CREATE TABLE dbo.ClosureAudit (
        ClosureId uniqueidentifier NOT NULL REFERENCES dbo.Closures(Id),
        Sequence tinyint NOT NULL CHECK (Sequence IN (1, 2)),
        Actor varchar(40) NOT NULL CHECK (LEN(Actor) >= 1),
        OccurredAt datetimeoffset(3) NOT NULL,
        CONSTRAINT PK_ClosureAudit PRIMARY KEY (ClosureId, Sequence)
    );
    CREATE ROLE fechaturma_app;
    GRANT SELECT, INSERT ON dbo.Closures TO fechaturma_app;
    GRANT UPDATE ON dbo.Closures (Version, ClosedAt) TO fechaturma_app;
    GRANT SELECT, INSERT ON dbo.ClosureRows TO fechaturma_app;
    GRANT SELECT, INSERT ON dbo.ClosureAudit TO fechaturma_app;
END;
COMMIT;
