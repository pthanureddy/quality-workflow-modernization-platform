using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Data;

namespace QualityWorkflow.Api.Tests;

internal sealed class DatabaseTestHelper : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public QualityDbContext DbContext { get; private set; } = null!;

    public static async Task<DatabaseTestHelper> CreateAsync()
    {
        var helper = new DatabaseTestHelper();
        await helper.connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QualityDbContext>()
            .UseSqlite(helper.connection)
            .Options;
        helper.DbContext = new QualityDbContext(options);
        await helper.DbContext.Database.EnsureCreatedAsync();
        return helper;
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await connection.DisposeAsync();
    }
}

