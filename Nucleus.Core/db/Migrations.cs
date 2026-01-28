using EvolveDb;
using Npgsql;

namespace Nucleus.db;

public static class Migrations
{
    public static void ApplyMigrations(this WebApplicationBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnectionString")
                                   ?? builder.Configuration["DatabaseConnectionString"];

        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment != null && environment != "Testing")
        {
            using NpgsqlConnection connection =
                new(connectionString ??
                    "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy");
            Evolve evolve = new(connection, Console.WriteLine)
            {
                Locations = ["db/migrations"],
                IsEraseDisabled = true
            };

            evolve.Migrate();
        }
    }
}