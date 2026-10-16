using System.Diagnostics;
using Backend.Infrastructure;
using Data;
using Service.Implementation;
using Service.Interface;

const string AngularCorsPolicy = "Angular";

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----
// SqlConnectionFactory reads ConnectionStrings:FreightLook; services inject IDbConnectionFactory
// and create their connection with it. The connection string is not stored in appsettings: set it with
// user secrets (development) or the ConnectionStrings__FreightLook environment variable (servers).
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("FreightLook")))
    throw new InvalidOperationException(
        "Connection string 'FreightLook' is not configured. Run: dotnet user-secrets set \"ConnectionStrings:FreightLook\" \"<connection string>\" " +
        "in the Backend folder, or set the ConnectionStrings__FreightLook environment variable.");
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// ---- Services (business logic + Dapper stored procedure calls) ----
builder.Services.AddSingleton<ISlabResolver, SlabResolver>();
builder.Services.AddScoped<IRateService, RateService>();
builder.Services.AddScoped<IAirportService, AirportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUploadService, UploadService>();

// ---- Rate sheet extraction (rule-based, no AI): one reader per file format ----
builder.Services.AddSingleton<IRateSheetReader, PdfRateSheetReader>();
builder.Services.AddSingleton<IRateSheetReader, ExcelRateSheetReader>();
builder.Services.AddSingleton<IExtractionService, ExtractionService>();

// ---- Web ----
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new() { Title = "FreightLook API", Version = "v1" }));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
    options.AddPolicy(AngularCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Swagger UI at /swagger (enabled in every environment; set "Swagger:Enabled": false to turn off).
if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FreightLook API v1");
        options.DocumentTitle = "FreightLook API";
    });
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(AngularCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
