# FreightLook

Air cargo rate search. Upload airline rate sheets (text PDF, Excel or CSV) and compare rates by route, with slab prices, surcharges and an estimated total for a shipment weight.

## Solution layout

| Project | Role |
|---|---|
| `Backend` | ASP.NET Core Web API: controllers, DI, CORS, Swagger, global exception handler |
| `Service` | Business logic: rate search, slab resolver, upload validation, rate sheet extraction |
| `Data` | Dapper repositories; **stored procedures only** |
| `Models` | Entities, DTOs, requests, responses |
| `AngularProject1` | Angular 22 + Angular Material UI |
| `Database` | SQL scripts and sample rate sheets (`Database/Samples`) |

Request flow: `Controller → Service → Data (stored procedure) → SQL Server`. Controllers never touch Data.

## Prerequisites

- .NET 10 SDK
- SQL Server or LocalDB (`(localdb)\MSSQLLocalDB`) and `sqlcmd`
- Node.js (current LTS) and npm

## Getting started

**1. Database**

```bat
Database\00_RunAll.cmd                       :: LocalDB
Database\00_RunAll.cmd "MYSERVER\SQLEXPRESS" :: or another server
```

Creates the `FreightLook` database, tables, stored procedures and sample data. The scripts can be re-run safely.

**2. API**

The connection string is `ConnectionStrings:FreightLook` in `Backend/appsettings.json` (default: LocalDB with Windows authentication). For other credentials, use user secrets or an environment variable rather than committing them:

```bat
set ConnectionStrings__FreightLook=Server=...;Database=FreightLook;User Id=...;Password=...
```

```bat
cd Backend
dotnet run --launch-profile http
```

The API runs on `http://localhost:5166`, and Swagger UI on `http://localhost:5166/swagger`.

**3. UI**

```bat
cd AngularProject1
npm install
npm start
```

Open `http://127.0.0.1:52153`. The API URL is set in `src/environments/environment*.ts`.

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/rates/search?origin=DEL&destination=DOH&weight=320` | Active rates for a route. `weight` is optional; with it, each rate is priced. |
| GET | `/api/airports` | Airports for the From/To autocomplete |
| POST | `/api/ratesheets/upload` | Multipart form field `file` (PDF/XLSX/XLS/CSV, max 10 MB) |
| GET | `/api/dashboard/stats` | Dashboard KPIs |

Errors are returned as RFC 7807 `ProblemDetails`: 400 for validation errors and 503 for database errors.

## Rate sheet upload

Extraction is rule-based. It uses no AI, so no API key is needed.

- **Supported:** text-based PDFs (PdfPig) and `.xlsx` / `.xls` / `.csv` (ExcelDataReader).
- **Not supported:** scanned or image-only PDFs. Export the sheet from the source system as a text PDF or a spreadsheet instead.
- **Header matching:** columns are matched by header name, so their order does not matter: `Loc, Airline, Cd, scr, Crny, Min., Normal, +45, +100, +250, +300, +500, +1000, FSC, SSC, Xray, Misc, CTG, AWBFEE, Ams-E/Awb, ...`
- **Sheet-level values:** destination and currency come from the `Destination : DOHA (DOH)` and `Currency : INR` lines above the table.
- **Surcharge cells:**
  - `127.000c` is per kg of chargeable weight; `100.000g` is per kg of gross weight.
  - `4.000c/ 300.000` is per kg with a minimum of 300.
  - Values in another currency (`0.250c /usd`) are skipped with a warning.
- **Skipped rows:** rows marked *CallCarrier*, or with no rates, are skipped with a warning.
- **Replacement:** a new upload replaces the active rate for the same **airline + route + commodity**. Search is not cached, so new rates show up immediately.

Sample files for testing are in `Database/Samples`.

## Slab logic

`SlabResolver` (plain C#) picks the highest slab the rate has whose minimum weight is ≤ W. Below the lowest numeric slab, it uses `M` (Normal).

| Weight (kg) | Slab |
|---|---|
| < 45 | M |
| 45–99 | +45 |
| 100–249 | +100 |
| 250–299 | +250 |
| 300–499 | +300 (if the rate has one, else +250) |
| 500–999 | +500 |
| ≥ 1000 | +1000 |

The estimated total is calculated as follows:
- **Freight** is the slab price × W, but never less than the minimum charge.
- **Per-kg surcharges** are amount × W, but never less than their own minimum.
- **Flat surcharges** (AMS, AWB fee) are added once per shipment.

## Tests

```bat
cd AngularProject1
npx ng test --watch=false
```

## Known limitations

- Free-text remarks under a rate table (e.g. "DG fee INR 6000 per AWB") are not captured.
- The UI takes one weight, so gross-weight (`g`) surcharges are estimated on that weight.
