using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Data;

namespace QualityWorkflow.Api.Tests;

public sealed class DataSeederTests
{
    [Fact]
    public async Task Seed_data_is_complete_and_idempotent_on_sqlite()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();

        await DataSeeder.SeedAsync(database.DbContext);
        await DataSeeder.SeedAsync(database.DbContext);

        Assert.Equal(3, await database.DbContext.Procedures.CountAsync());
        Assert.Equal(1, await database.DbContext.CorrectiveActions.CountAsync());
        Assert.Equal(3, await database.DbContext.AuditEntries.CountAsync());
    }
}

