using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts.Events;

public sealed record PostCreated(PostId PostId, UserId AuthorId) : IDomainEvent;
