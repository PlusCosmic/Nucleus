using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nucleus.Models;

// This class is used by EF Core tools at design time (migrations, bundle generation)
// It provides a way to create the DbContext when there's no runtime environment
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NucleusDbContext>
{
    public NucleusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NucleusDbContext>();
        
        // Use a dummy connection string for design-time operations
        // The actual connection string will be provided at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy");
        
        return new NucleusDbContext(optionsBuilder.Options);
    }
}