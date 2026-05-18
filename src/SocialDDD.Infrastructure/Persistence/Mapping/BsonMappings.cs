using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Mapping;

internal static class BsonMappings
{
    private static int _registered;

    internal static void Register()
    {
        // Guard: MongoDbContext is singleton but protect against double registration.
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;

        // Register global serializers for every value-object type.
        // AutoMap will use these automatically when it encounters any property
        // of that type — including the Id property inherited from Entity<TId> —
        // without needing per-member SetSerializer calls inside the class map.
        BsonSerializer.RegisterSerializer(new UserIdSerializer());
        BsonSerializer.RegisterSerializer(new PostIdSerializer());
        BsonSerializer.RegisterSerializer(new UsernameSerializer());
        BsonSerializer.RegisterSerializer(new EmailSerializer());
        BsonSerializer.RegisterSerializer(new PasswordHashSerializer());
        BsonSerializer.RegisterSerializer(new PostContentSerializer());
        BsonSerializer.RegisterSerializer(new HandleSerializer());
        BsonSerializer.RegisterSerializer(new DisplayNameSerializer());

        // AutoMap traverses base classes. When it processes User it first
        // registers Entity<UserId>, where Id IS the declaring type, so the
        // Id convention correctly sets IdMemberMap there. User inherits it.
        BsonClassMap.RegisterClassMap<User>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(u => u.Handle).SetElementName("handle");
            cm.MapMember(u => u.DisplayName).SetElementName("displayName");
        });

        BsonClassMap.RegisterClassMap<Post>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(p => p.LikedBy).SetElementName("likedBy");
            cm.MapMember(p => p.ParentPostId).SetElementName("parentPostId");
            cm.MapMember(p => p.Mentions).SetElementName("mentions");
            cm.MapMember(p => p.Hashtags).SetElementName("hashtags");
        });
    }
}

// ---------- serializers -------------------------------------------------------

internal sealed class UserIdSerializer : SerializerBase<UserId>
{
    public override UserId Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => UserId.From(Guid.Parse(ctx.Reader.ReadString()));

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, UserId value)
        => ctx.Writer.WriteString(value.Value.ToString());
}

internal sealed class PostIdSerializer : SerializerBase<PostId>
{
    public override PostId Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => PostId.From(Guid.Parse(ctx.Reader.ReadString()));

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PostId value)
        => ctx.Writer.WriteString(value.Value.ToString());
}

internal sealed class UsernameSerializer : SerializerBase<Username>
{
    public override Username Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Username value)
        => ctx.Writer.WriteString(value.Value);
}

internal sealed class EmailSerializer : SerializerBase<Email>
{
    public override Email Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Email value)
        => ctx.Writer.WriteString(value.Value);
}

internal sealed class PasswordHashSerializer : SerializerBase<PasswordHash>
{
    public override PasswordHash Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PasswordHash value)
        => ctx.Writer.WriteString(value.Value);
}

internal sealed class PostContentSerializer : SerializerBase<PostContent>
{
    public override PostContent Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PostContent value)
        => ctx.Writer.WriteString(value.Value);
}

internal sealed class HandleSerializer : SerializerBase<Handle>
{
    public override Handle Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Handle value)
        => ctx.Writer.WriteString(value.Value);
}

internal sealed class DisplayNameSerializer : SerializerBase<DisplayName>
{
    public override DisplayName Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
        => new(ctx.Reader.ReadString());

    public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, DisplayName value)
        => ctx.Writer.WriteString(value.Value);
}
