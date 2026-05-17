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

        EnsureIndexes();
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Post> Posts => _database.GetCollection<Post>("posts");

    private void EnsureIndexes()
    {
        var handleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending("handle"),
            new CreateIndexOptions { Unique = true, Background = true, Name = "handle_unique" });

        Users.Indexes.CreateOne(handleIndex);
    }
}
