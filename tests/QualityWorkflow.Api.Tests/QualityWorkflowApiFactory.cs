using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QualityWorkflow.Api.Data;

namespace QualityWorkflow.Api.Tests;

internal sealed class QualityWorkflowApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public QualityWorkflowApiFactory()
    {
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DatabaseInitialization", "None");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<QualityDbContext>>();
            services.RemoveAll<QualityDbContext>();
            services.AddDbContext<QualityDbContext>(options => options.UseSqlite(connection));
        });
    }

    public HttpClient CreateInitializedClient()
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<QualityDbContext>().Database.EnsureCreated();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
