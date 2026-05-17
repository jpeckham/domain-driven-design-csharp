namespace SocialDDD.Infrastructure.Persistence;

public sealed class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
