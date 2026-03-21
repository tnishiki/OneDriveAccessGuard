using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace OneDriveAccessGuard.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AccessGuardDbContext>
{
    public AccessGuardDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneDriveAccessGuard", "data.db");

        var options = new DbContextOptionsBuilder<AccessGuardDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new AccessGuardDbContext(options);
    }
}
