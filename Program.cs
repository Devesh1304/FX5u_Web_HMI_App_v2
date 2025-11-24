using FX5u_Web_HMI_App;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FX5u_Web_HMI_App.Hubs;
using FX5u_Web_HMI_App.BackgroundServices;
// --- USING STATEMENTS FOR DATABASE ---
using Microsoft.EntityFrameworkCore;
// --- USING STATEMENTS FOR LOCALIZATION ---
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using FX5u_Web_HMI_App.Data;


var builder = WebApplication.CreateBuilder(args);

// --- 1. ADD LOCALIZATION SERVICES ---
var supportedCultures = new[]
{
    new CultureInfo("en-US"), // English (United States)
    new CultureInfo("gu-IN")  // Gujarati (India)
};

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});
// --- END LOCALIZATION ---


// --- 2. ADD OTHER SERVICES ---
builder.Services.AddRazorPages();

// Add Database Context
builder.Services.AddDbContext<LogDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add HMI Services
builder.Services.AddSingleton<ISLMPService, SLMPService>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<PlcMonitorService>();


var app = builder.Build();

// --- 3. CONFIGURE THE HTTP REQUEST PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// --- 4. ADD LOCALIZATION MIDDLEWARE ---
// This must be after UseRouting() and before UseAuthorization()
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
// --- END LOCALIZATION ---

app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapHub<PlcHub>("/plcHub");

// --- 5. AUTOMATICALLY CREATE/UPDATE THE DATABASE ON STARTUP ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<LogDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}
app.Run();