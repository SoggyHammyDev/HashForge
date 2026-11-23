using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HashForge.Persistence.Database;

/// <summary>Lets the EF Core tooling build the context when generating migrations.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HashForgeDbContext>
{
    public HashForgeDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<HashForgeDbContext>().UseNpgsql("Host=localhost;Database=hashforge").Options);
}
