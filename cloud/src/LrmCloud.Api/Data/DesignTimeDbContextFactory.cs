using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LrmCloud.Api.Data;

/// <summary>
/// Factory for creating AppDbContext at design time (migrations).
/// This avoids requiring config.json when running dotnet ef commands.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a placeholder connection string for design-time operations
        // The actual connection string is loaded from config.json at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=lrmcloud_migrations;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options);
    }
}
