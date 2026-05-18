using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts.Events;

public sealed record PostLiked(PostId PostId, Handle LikedByHandle) : IDomainEvent;
