using MongoDB.Driver;
using SmartKostanay.Services;
using static SmartKostanay.Services.CadastreService;

var builder = WebApplication.CreateBuilder(args);

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var dbName = builder.Configuration.GetSection("DatabaseSettings:DatabaseName").Value;

// 2. Регистрируем IMongoClient как Singleton (один на всё приложение)
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

builder.Services.AddScoped<CadastreService>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new CadastreService(client, dbName);
});

// Регистрация сервиса с автоматической поддержкой Cookies
builder.Services.AddHttpClient<EgknIntegrationService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer(),
        // Если гос. сервер капризничает с SSL, можно добавить:
        // ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true 
    });


builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<CoordinateConverter>();
builder.Services.AddHttpClient<EgknIntegrationService>();
builder.Services.AddScoped<EgknIntegrationService>();

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