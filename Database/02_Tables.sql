-- FreightLook: tables (idempotent)
USE FreightLook;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Airline', N'U') IS NULL
CREATE TABLE dbo.Airline
(
    AirlineId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Airline PRIMARY KEY,
    Code        NVARCHAR(3)   NOT NULL,          -- IATA 2-char (or ICAO 3-char)
    Name        NVARCHAR(100) NOT NULL,
    CreatedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_Airline_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Airline_Code UNIQUE (Code)
);
GO

IF OBJECT_ID(N'dbo.Airport', N'U') IS NULL
CREATE TABLE dbo.Airport
(
    AirportId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Airport PRIMARY KEY,
    Code        CHAR(3)       NOT NULL,          -- IATA airport code
    Name        NVARCHAR(150) NOT NULL,
    City        NVARCHAR(100) NULL,
    Country     NVARCHAR(100) NULL,
    CONSTRAINT UQ_Airport_Code UNIQUE (Code)
);
GO

IF OBJECT_ID(N'dbo.Rate', N'U') IS NULL
CREATE TABLE dbo.Rate
(
    RateId                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rate PRIMARY KEY,
    AirlineId             INT            NOT NULL CONSTRAINT FK_Rate_Airline     REFERENCES dbo.Airline(AirlineId),
    OriginAirportId       INT            NOT NULL CONSTRAINT FK_Rate_Origin      REFERENCES dbo.Airport(AirportId),
    DestinationAirportId  INT            NOT NULL CONSTRAINT FK_Rate_Destination REFERENCES dbo.Airport(AirportId),
    -- Special commodity / product (Courier, DGR, Perishable, ...). Empty = general cargo.
    Commodity             NVARCHAR(30)   NOT NULL CONSTRAINT DF_Rate_Commodity DEFAULT N'',
    Currency             CHAR(3)        NOT NULL CONSTRAINT DF_Rate_Currency DEFAULT 'USD',
    MinimumCharge         DECIMAL(18,2)  NOT NULL CONSTRAINT CK_Rate_MinimumCharge CHECK (MinimumCharge >= 0),
    SourceFile            NVARCHAR(260)  NULL,
    IsActive              BIT            NOT NULL CONSTRAINT DF_Rate_IsActive DEFAULT 1,
    CreatedAt             DATETIME2(0)   NOT NULL CONSTRAINT DF_Rate_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Rate_OriginDest CHECK (OriginAirportId <> DestinationAirportId)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Rate_Route_Active')
    CREATE INDEX IX_Rate_Route_Active
        ON dbo.Rate (OriginAirportId, DestinationAirportId, IsActive)
        INCLUDE (AirlineId, MinimumCharge, Currency);
GO

-- Only one active rate per airline / route / commodity
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Rate_Active')
    CREATE UNIQUE INDEX UX_Rate_Active
        ON dbo.Rate (AirlineId, OriginAirportId, DestinationAirportId, Commodity)
        WHERE IsActive = 1;
GO

IF OBJECT_ID(N'dbo.RateSlab', N'U') IS NULL
CREATE TABLE dbo.RateSlab
(
    RateSlabId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RateSlab PRIMARY KEY,
    RateId      INT           NOT NULL CONSTRAINT FK_RateSlab_Rate REFERENCES dbo.Rate(RateId) ON DELETE CASCADE,
    SlabCode    VARCHAR(5)    NOT NULL CONSTRAINT CK_RateSlab_Code CHECK (SlabCode IN ('M','+45','+100','+250','+300','+500','+1000')),
    PricePerKg  DECIMAL(18,4) NOT NULL CONSTRAINT CK_RateSlab_Price CHECK (PricePerKg >= 0),
    CONSTRAINT UQ_RateSlab_Rate_Code UNIQUE (RateId, SlabCode)
);
GO

IF OBJECT_ID(N'dbo.Surcharge', N'U') IS NULL
CREATE TABLE dbo.Surcharge
(
    SurchargeId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Surcharge PRIMARY KEY,
    RateId         INT           NOT NULL CONSTRAINT FK_Surcharge_Rate REFERENCES dbo.Rate(RateId) ON DELETE CASCADE,
    SurchargeType  VARCHAR(5)    NOT NULL CONSTRAINT CK_Surcharge_Type CHECK (SurchargeType IN ('FSC','SSC','SEC','XRAY','MISC','CTG','AMS','AWB')),
    Amount         DECIMAL(18,4) NOT NULL CONSTRAINT CK_Surcharge_Amount CHECK (Amount >= 0),
    -- Minimum charge for per-kg surcharges (e.g. "4.000c / 300.000"); NULL = no minimum
    MinAmount      DECIMAL(18,4) NULL     CONSTRAINT CK_Surcharge_MinAmount CHECK (MinAmount >= 0),
    -- KG = charged per chargeable kg, SHIPMENT = flat per AWB
    Basis          VARCHAR(8)    NOT NULL CONSTRAINT CK_Surcharge_Basis CHECK (Basis IN ('KG','SHIPMENT')),
    CONSTRAINT UQ_Surcharge_Rate_Type UNIQUE (RateId, SurchargeType)
);
GO

-- Upload audit (feeds the dashboard "last upload" KPI)
IF OBJECT_ID(N'dbo.RateSheetUpload', N'U') IS NULL
CREATE TABLE dbo.RateSheetUpload
(
    RateSheetUploadId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RateSheetUpload PRIMARY KEY,
    FileName          NVARCHAR(260) NOT NULL,
    ParsedCount       INT           NOT NULL,
    SavedCount        INT           NOT NULL,
    RejectedCount     INT           NOT NULL,
    UploadedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_RateSheetUpload_UploadedAt DEFAULT SYSUTCDATETIME()
);
GO
