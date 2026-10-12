var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/whoami", () => Results.Ok(new
{
    Hostname = Environment.MachineName,
    Replica = Environment.GetEnvironmentVariable("CONTAINER_APP_REPLICA_NAME"),
    Time = DateTimeOffset.UtcNow
}));

app.MapGet("/work", async (ILogger<Program> logger) =>
{
    var started = DateTimeOffset.UtcNow;

    logger.LogInformation(
        "Work started at {Started} on host {Host}",
        started, Environment.MachineName);

    await Task.Delay(TimeSpan.FromSeconds(3));

    logger.LogInformation(
        "Work completed at {Completed} on host {Host}",
        DateTimeOffset.UtcNow, Environment.MachineName);

    return Results.Ok(new
    {
        Hostname = Environment.MachineName,
        Started = started,
        Completed = DateTimeOffset.UtcNow
    });
});

app.Run();
