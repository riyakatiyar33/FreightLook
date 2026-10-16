-- FreightLook: reference + sample data (safe to re-run)
USE FreightLook;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.Airport AS t
USING (VALUES
    ('BOM', N'Chhatrapati Shivaji Maharaj Intl', N'Mumbai',        N'India'),
    ('DEL', N'Indira Gandhi Intl',               N'Delhi',         N'India'),
    ('MAA', N'Chennai Intl',                     N'Chennai',       N'India'),
    ('BLR', N'Kempegowda Intl',                  N'Bengaluru',     N'India'),
    ('HYD', N'Rajiv Gandhi Intl',                N'Hyderabad',     N'India'),
    ('CCU', N'Netaji Subhas Chandra Bose Intl',  N'Kolkata',       N'India'),
    ('DXB', N'Dubai Intl',                       N'Dubai',         N'United Arab Emirates'),
    ('DOH', N'Hamad Intl',                       N'Doha',          N'Qatar'),
    ('AUH', N'Zayed Intl',                       N'Abu Dhabi',     N'United Arab Emirates'),
    ('SIN', N'Changi',                           N'Singapore',     N'Singapore'),
    ('HKG', N'Hong Kong Intl',                   N'Hong Kong',     N'Hong Kong'),
    ('PVG', N'Shanghai Pudong Intl',             N'Shanghai',      N'China'),
    ('NRT', N'Narita Intl',                      N'Tokyo',         N'Japan'),
    ('ICN', N'Incheon Intl',                     N'Seoul',         N'South Korea'),
    ('FRA', N'Frankfurt',                        N'Frankfurt',     N'Germany'),
    ('LHR', N'Heathrow',                         N'London',        N'United Kingdom'),
    ('CDG', N'Charles de Gaulle',                N'Paris',         N'France'),
    ('AMS', N'Schiphol',                         N'Amsterdam',     N'Netherlands'),
    ('IST', N'Istanbul',                         N'Istanbul',      N'Turkey'),
    ('JFK', N'John F. Kennedy Intl',             N'New York',      N'United States'),
    ('ORD', N'O''Hare Intl',                     N'Chicago',       N'United States'),
    ('LAX', N'Los Angeles Intl',                 N'Los Angeles',   N'United States'),
    ('ATL', N'Hartsfield-Jackson Atlanta Intl',  N'Atlanta',       N'United States'),
    ('SYD', N'Sydney Kingsford Smith',           N'Sydney',        N'Australia'),
    ('LON', N'London (all airports)',            N'London',        N'United Kingdom'),
    ('PAR', N'Paris (all airports)',             N'Paris',         N'France'),
    ('JNB',N'O. R. Tambo Intl',                 N'Johannesburg',  N'South Africa'),
    ('NBO', N'Jomo Kenyatta Intl',               N'Nairobi',       N'Kenya')
) AS s (Code, Name, City, Country)
ON t.Code = s.Code
WHEN MATCHED AND t.Name = t.Code THEN            -- fill in airports auto-created by uploads
    UPDATE SET Name = s.Name, City = s.City, Country = s.Country
WHEN NOT MATCHED THEN
    INSERT (Code, Name, City, Country) VALUES (s.Code, s.Name, s.City, s.Country);
GO

MERGE dbo.Airline AS t
USING (VALUES
    ('EK', N'Emirates SkyCargo'),
    ('QR', N'Qatar Airways Cargo'),
    ('LH', N'Lufthansa Cargo'),
    ('SQ', N'Singapore Airlines Cargo'),
    ('TK', N'Turkish Cargo'),
    ('AI', N'Air India Cargo')
) AS s (Code, Name)
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name) VALUES (s.Code, s.Name);
GO

-- Sample rates (only when the Rate table is empty) so search works before any upload
IF NOT EXISTS (SELECT 1 FROM dbo.Rate)
BEGIN
    DECLARE @RateId INT;
    DECLARE @Slabs dbo.RateSlabTableType, @Sur dbo.SurchargeTableType;

    -- EK BOM -> FRA
    INSERT @Slabs VALUES ('M',3.60),('+45',3.10),('+100',2.85),('+250',2.60),('+500',2.40),('+1000',2.25);
    INSERT @Sur (SurchargeType, Amount, Basis) VALUES ('FSC',0.95,'KG'),('SSC',0.15,'KG'),('XRAY',0.05,'KG'),('AWB',15,'SHIPMENT'),('AMS',0,'SHIPMENT');
    EXEC dbo.usp_Rate_Insert 'EK', N'Emirates SkyCargo', 'BOM', 'FRA', N'', 'USD', 85, N'seed', @Slabs, @Sur, @RateId OUTPUT;
    DELETE @Slabs; DELETE @Sur;

    -- QR BOM -> FRA
    INSERT @Slabs VALUES ('M',3.40),('+45',2.95),('+100',2.70),('+250',2.50),('+500',2.30),('+1000',2.10);
    INSERT @Sur (SurchargeType, Amount, Basis) VALUES ('FSC',1.05,'KG'),('SEC',0.12,'KG'),('XRAY',0.04,'KG'),('MISC',10,'SHIPMENT'),('AWB',12,'SHIPMENT');
    EXEC dbo.usp_Rate_Insert 'QR', N'Qatar Airways Cargo', 'BOM', 'FRA', N'', 'USD', 80, N'seed', @Slabs, @Sur, @RateId OUTPUT;
    DELETE @Slabs; DELETE @Sur;

    -- LH BOM -> FRA
    INSERT @Slabs VALUES ('M',3.80),('+45',3.25),('+100',2.90),('+250',2.55),('+500',2.35),('+1000',2.20);
    INSERT @Sur (SurchargeType, Amount, Basis) VALUES ('FSC',1.10,'KG'),('SSC',0.18,'KG'),('AWB',20,'SHIPMENT');
    EXEC dbo.usp_Rate_Insert 'LH', N'Lufthansa Cargo', 'BOM', 'FRA', N'', 'EUR', 90, N'seed', @Slabs, @Sur, @RateId OUTPUT;
    DELETE @Slabs; DELETE @Sur;

    -- SQ DEL -> SIN
    INSERT @Slabs VALUES ('M',2.10),('+45',1.80),('+100',1.65),('+250',1.50),('+500',1.40);
    INSERT @Sur (SurchargeType, Amount, Basis) VALUES ('FSC',0.70,'KG'),('SSC',0.10,'KG'),('AWB',10,'SHIPMENT');
    EXEC dbo.usp_Rate_Insert 'SQ', N'Singapore Airlines Cargo', 'DEL', 'SIN', N'', 'USD', 60, N'seed', @Slabs, @Sur, @RateId OUTPUT;
    DELETE @Slabs; DELETE @Sur;

    -- TK DEL -> JFK
    INSERT @Slabs VALUES ('M',5.20),('+45',4.60),('+100',4.20),('+250',3.90),('+500',3.60),('+1000',3.40);
    INSERT @Sur (SurchargeType, Amount, Basis) VALUES ('FSC',1.25,'KG'),('SEC',0.15,'KG'),('XRAY',0.06,'KG'),('AMS',25,'SHIPMENT'),('AWB',15,'SHIPMENT');
    EXEC dbo.usp_Rate_Insert 'TK', N'Turkish Cargo', 'DEL', 'JFK', N'', 'USD', 120, N'seed', @Slabs, @Sur, @RateId OUTPUT;
END
GO
