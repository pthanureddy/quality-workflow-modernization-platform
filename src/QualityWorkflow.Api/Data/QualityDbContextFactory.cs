using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualityWorkflow.Api.Data;

public sealed class QualityDbContextFactory : IDesignTimeDbContextFactory<QualityDbContext>
{
    public QualityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QualityDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QualityWorkflowDesign;Trusted_Connection=True;TrustServerCertificate=true")
            .Options;

        return new QualityDbContext(options);
    }
}
