using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Infrastructure;

public static class DevelopmentDatabaseInitializer
{
    public static async Task EnsureDevelopmentDatabaseMigratedAsync(this IServiceProvider services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MuseumDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
