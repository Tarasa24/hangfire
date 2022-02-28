using Hangfire;
using Hangfire.MySql;
using Hangfire.MemoryStorage;
using Hangfire.Console;
using Prometheus;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);;
var services = builder.Services;

services.AddHangfire(configuration => {
    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseColouredConsoleLogProvider()
        .UseConsole();
    if (builder.Environment.IsDevelopment())
    {
        Env.Load();
        configuration.UseMemoryStorage();
    } 
    else
        configuration
            .UseStorage(
            new MySqlStorage(
                Environment.GetEnvironmentVariable("MYSQL_CS"),
                new MySqlStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    JobExpirationCheckInterval = TimeSpan.FromHours(1),
                    CountersAggregateInterval = TimeSpan.FromMinutes(5),
                    PrepareSchemaIfNecessary = true,
                    DashboardJobListLimit = 50000,
                    TransactionTimeout = TimeSpan.FromMinutes(1),
                    TablesPrefix = "Hangfire"
                }));
    });
services.AddHangfireServer();
services.AddMvc();

var app = builder.Build();


app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHangfireDashboard("", new DashboardOptions
    {
        AppPath = "https://tarasa24.dev"
    });
    endpoints.MapMetrics();

});


//BackgroundJob.Enqueue(() => Console.WriteLine("Hello, world!"));
RecurringJob.AddOrUpdate("Occupancy scrape", () => hangfire.Jobs.Occupancy.Run(null), "*/15 * * * *");
RecurringJob.AddOrUpdate("Update Github pinned projects", () => hangfire.Jobs.GithubPinnedProjects.Run(null), "*/10 * * * *");

app.Run();