using MongoDB.Driver;

namespace Nucleus.Dropzone;

public class DropzoneService
{
    public async Task<ShareGroup> GetGroup(IMongoCollection<ShareGroup> collection, string pin)
    {
        FilterDefinition<ShareGroup> filter = Builders<ShareGroup>.Filter.Eq(sg => sg.GroupPin, pin);
        List<ShareGroup> foundGroups = await collection.Find(filter).ToListAsync();
        List<ShareGroup> activeGroups = foundGroups.Where(sg => sg.ExpiresAt > DateTimeOffset.UtcNow).ToList();
        if (activeGroups.Count == 1)
        {
            return activeGroups.First();
        }

        ShareGroup shareGroup = new() { GroupPin = pin, ExpiresAt = DateTimeOffset.UtcNow.AddHours(12) };
        await collection.InsertOneAsync(shareGroup);
        return shareGroup;
    }
}