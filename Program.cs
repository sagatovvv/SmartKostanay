using MongoDB.Driver;
using SmartKostanay.Services;
using SmartKostanay.Helpers;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var dbName = builder.Configuration.GetSection("DatabaseSettings:DatabaseName").Value;

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

builder.Services.AddScoped<CadastreService>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new CadastreService(client, dbName);
});

// 2. Регистрация EgknScraperService с поддержкой COOKIES и GZIP
builder.Services.AddHttpClient<EgknScraperService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        // Критически важно для получения данных (распаковка ответа сервера)
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// 3. Регистрация EgknIntegrationService
builder.Services.AddHttpClient<EgknIntegrationService>();

// 4. Остальные сервисы
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<CoordinateConverter>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();