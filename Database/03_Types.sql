-- FreightLook: table-valued parameter types used by usp_Rate_Insert
USE FreightLook;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF TYPE_ID(N'dbo.RateSlabTableType') IS NULL
CREATE TYPE dbo.RateSlabTableType AS TABLE
(
    SlabCode    VARCHAR(5)    NOT NULL,
    PricePerKg  DECIMAL(18,4) NOT NULL
);
GO

IF TYPE_ID(N'dbo.SurchargeTableType') IS NULL
CREATE TYPE dbo.SurchargeTableType AS TABLE
(
    SurchargeType VARCHAR(5)    NOT NULL,
    Amount        DECIMAL(18,4) NOT NULL,
    MinAmount     DECIMAL(18,4) NULL,
    Basis         VARCHAR(8)    NOT NULL
);
GO
