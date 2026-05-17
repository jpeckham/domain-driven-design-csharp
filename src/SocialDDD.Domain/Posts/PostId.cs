namespace SocialDDD.Domain.Posts;

public sealed record PostId(Guid Value)
{
    public static PostId New() => new(Guid.NewGuid());
    public static PostId From(Guid value) => new(value);
}
