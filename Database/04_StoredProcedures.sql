-- FreightLook: stored procedures (idempotent via CREATE OR ALTER)
USE FreightLook;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   usp_Rate_Insert
   Saves one rate (airline + route + commodity) with its slabs and surcharges atomically.
   - Airline is upserted by code.
   - Unknown airports are created with Name = Code so uploads never fail on them.
   - Any previously active rate for the same airline/origin/destination/commodity
     is deactivated, so the newest upload wins.
--------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Rate_Insert
    @AirlineCode      NVARCHAR(3),
    @AirlineName      NVARCHAR(100) = NULL,
    @OriginCode       CHAR(3),
    @DestinationCode  CHAR(3),
    @Commodity        NVARCHAR(30) = N'',
    @Currency        CHAR(3) = 'USD',
    @MinimumCharge    DECIMAL(18,2),
    @SourceFile       NVARCHAR(260) = NULL,
    @Slabs            dbo.RateSlabTableType  READONLY,
    @Surcharges       dbo.SurchargeTableType READONLY,
    @RateId           INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @AirlineCode     = UPPER(LTRIM(RTRIM(@AirlineCode)));
    SET @OriginCode      = UPPER(LTRIM(RTRIM(@OriginCode)));
    SET @DestinationCode = UPPER(LTRIM(RTRIM(@DestinationCode)));
    SET @Commodity       = COALESCE(LTRIM(RTRIM(@Commodity)), N'');

    BEGIN TRANSACTION;

    DECLARE @AirlineId INT, @OriginId INT, @DestinationId INT;

    SELECT @AirlineId = AirlineId FROM dbo.Airline WITH (UPDLOCK, HOLDLOCK) WHERE Code = @AirlineCode;
    IF @AirlineId IS NULL
    BEGIN
        INSERT dbo.Airline (Code, Name) VALUES (@AirlineCode, COALESCE(NULLIF(LTRIM(RTRIM(@AirlineName)), N''), @AirlineCode));
        SET @AirlineId = SCOPE_IDENTITY();
    END
    ELSE IF NULLIF(LTRIM(RTRIM(@AirlineName)), N'') IS NOT NULL
        UPDATE dbo.Airline SET Name = LTRIM(RTRIM(@AirlineName)) WHERE AirlineId = @AirlineId AND Name = Code;

    SELECT @OriginId = AirportId FROM dbo.Airport WITH (UPDLOCK, HOLDLOCK) WHERE Code = @OriginCode;
    IF @OriginId IS NULL
    BEGIN
        INSERT dbo.Airport (Code, Name) VALUES (@OriginCode, @OriginCode);
        SET @OriginId = SCOPE_IDENTITY();
    END

    SELECT @DestinationId = AirportId FROM dbo.Airport WITH (UPDLOCK, HOLDLOCK) WHERE Code = @DestinationCode;
    IF @DestinationId IS NULL
    BEGIN
        INSERT dbo.Airport (Code, Name) VALUES (@DestinationCode, @DestinationCode);
        SET @DestinationId = SCOPE_IDENTITY();
    END

    UPDATE dbo.Rate
       SET IsActive = 0
     WHERE AirlineId = @AirlineId
       AND OriginAirportId = @OriginId
       AND DestinationAirportId = @DestinationId
       AND Commodity = @Commodity
       AND IsActive = 1;

    INSERT dbo.Rate (AirlineId, OriginAirportId, DestinationAirportId, Commodity, Currency, MinimumCharge, SourceFile)
    VALUES (@AirlineId, @OriginId, @DestinationId, @Commodity, UPPER(COALESCE(@Currency, 'USD')), @MinimumCharge, @SourceFile);
    SET @RateId = SCOPE_IDENTITY();

    INSERT dbo.RateSlab (RateId, SlabCode, PricePerKg)
    SELECT @RateId, SlabCode, PricePerKg FROM @Slabs;

    INSERT dbo.Surcharge (RateId, SurchargeType, Amount, MinAmount, Basis)
    SELECT @RateId, SurchargeType, Amount, MinAmount, Basis FROM @Surcharges;

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------------
   usp_Rate_Search
   Returns three result sets for active rates on a route:
     1) rates, 2) slabs, 3) surcharges.
   NULL origin/destination means "any".
--------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Rate_Search
    @Origin       CHAR(3) = NULL,
    @Destination  CHAR(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Origin      = NULLIF(UPPER(LTRIM(RTRIM(@Origin))), '');
    SET @Destination = NULLIF(UPPER(LTRIM(RTRIM(@Destination))), '');

    DECLARE @Rates TABLE (RateId INT PRIMARY KEY);

    INSERT @Rates (RateId)
    SELECT r.RateId
      FROM dbo.Rate r
      JOIN dbo.Airport o ON o.AirportId = r.OriginAirportId
      JOIN dbo.Airport d ON d.AirportId = r.DestinationAirportId
     WHERE r.IsActive = 1
       AND (@Origin      IS NULL OR o.Code = @Origin)
       AND (@Destination IS NULL OR d.Code = @Destination);

    SELECT r.RateId,
           a.AirlineId, a.Code AS AirlineCode, a.Name AS AirlineName,
           o.Code AS OriginCode, o.Name AS OriginName,
           d.Code AS DestinationCode, d.Name AS DestinationName,
           r.Commodity, r.Currency, r.MinimumCharge, r.CreatedAt
      FROM @Rates x
      JOIN dbo.Rate    r ON r.RateId    = x.RateId
      JOIN dbo.Airline a ON a.AirlineId = r.AirlineId
      JOIN dbo.Airport o ON o.AirportId = r.OriginAirportId
      JOIN dbo.Airport d ON d.AirportId = r.DestinationAirportId
     ORDER BY a.Name, o.Code, d.Code, r.Commodity;

    SELECT s.RateSlabId, s.RateId, s.SlabCode, s.PricePerKg
      FROM dbo.RateSlab s
      JOIN @Rates x ON x.RateId = s.RateId;

    SELECT c.SurchargeId, c.RateId, c.SurchargeType, c.Amount, c.MinAmount, c.Basis
      FROM dbo.Surcharge c
      JOIN @Rates x ON x.RateId = c.RateId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Airport_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AirportId, Code, Name, City, Country
      FROM dbo.Airport
     ORDER BY Code;
END
GO

/* Supporting procedures for the upload audit and the dashboard */
CREATE OR ALTER PROCEDURE dbo.usp_RateSheetUpload_Insert
    @FileName       NVARCHAR(260),
    @ParsedCount    INT,
    @SavedCount     INT,
    @RejectedCount  INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RateSheetUpload (FileName, ParsedCount, SavedCount, RejectedCount)
    VALUES (@FileName, @ParsedCount, @SavedCount, @RejectedCount);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS RateSheetUploadId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_GetStats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(DISTINCT AirlineId) FROM dbo.Rate WHERE IsActive = 1)                                   AS AirlineCount,
        (SELECT COUNT(*) FROM (SELECT DISTINCT OriginAirportId, DestinationAirportId
                                 FROM dbo.Rate WHERE IsActive = 1) t)                                         AS RouteCount,
        (SELECT COUNT(*) FROM dbo.Rate WHERE IsActive = 1)                                                    AS RateCount,
        u.FileName      AS LastUploadFileName,
        u.UploadedAt    AS LastUploadAt,
        u.SavedCount    AS LastUploadSavedCount
    FROM (SELECT 1 AS One) dummy
    OUTER APPLY (SELECT TOP (1) FileName, UploadedAt, SavedCount
                   FROM dbo.RateSheetUpload ORDER BY UploadedAt DESC, RateSheetUploadId DESC) u;
END
GO
