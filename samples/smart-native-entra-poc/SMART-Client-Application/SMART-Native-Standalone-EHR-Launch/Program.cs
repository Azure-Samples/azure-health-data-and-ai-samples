using SmartOnFhirDemo.Infrastructure;
using SmartOnFhirDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddHttpClient();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<SmartConfigService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FhirService>();
builder.Services.AddScoped<BackendTokenService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
