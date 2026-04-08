using MongoDB.Driver;
using SmartKostanay.Services;

var builder = WebApplication.CreateBuilder(args);

// --- НАСТРОЙКА MONGODB ---

// 1. Извлекаем строку подключения из appsettings.json
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var dbName = builder.Configuration.GetSection("DatabaseSettings:DatabaseName").Value;

// 2. Регистрируем IMongoClient как Singleton (один на всё приложение)
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

// 3. Регистрируем CadastreService, передавая в него клиент и имя базы
builder.Services.AddScoped<CadastreService>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new CadastreService(client, dbName);
});


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