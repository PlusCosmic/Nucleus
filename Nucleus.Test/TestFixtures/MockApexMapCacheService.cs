using Nucleus.ApexLegends;
using Nucleus.Data.ApexLegends.Models;

namespace Nucleus.Test.TestFixtures;

/// <summary>
/// Mock implementation of IApexMapCacheService for testing.
/// Stores map rotation data in memory instead of Redis.
/// </summary>
public class MockApexMapCacheService : IApexMapCacheService
{
    private CurrentMapRotation? _cachedRotation;

    public Task SetMapRotationAsync(CurrentMapRotation rotation)
    {
        _cachedRotation = rotation;
        return Task.CompletedTask;
    }

    public Task<CurrentMapRotation?> GetMapRotationAsync()
    {
        return Task.FromResult(_cachedRotation);
    }
}
