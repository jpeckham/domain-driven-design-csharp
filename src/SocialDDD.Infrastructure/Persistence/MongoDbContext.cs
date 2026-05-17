using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Persistence.Mapping;

namespace SocialDDD.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoSettings> options)
    {
        BsonMappings.Register();

        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Post> Posts => _database.GetCollection<Post>("posts");
}
