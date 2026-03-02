using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TrunoWebApp.Components;
using TrunoWebApp.Data;
using TrunoWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.DetailedErrors = true);

builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlServer("Server=tcp:grocery-stores-db-server.database.windows.net,1433;Initial Catalog=truno-app;Persist Security Info=False;User ID=serveradmin;Password=hg!fx249kjhfgAtrz913;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=100;"));


builder.Services.AddHttpClient("TruCommerce", client =>
{
    client.BaseAddress = new Uri("https://test.trucommerce.com/v2/");
    client.Timeout = TimeSpan.FromSeconds(120);

    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("epic-test-ecom:3M4R#mUEHVYbG8ZV"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Epic-Test");
});

builder.Services.AddScoped<ItemMasterSyncService>();
builder.Services.AddScoped<ItemMasterReadService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
