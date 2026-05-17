using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts.Events;

public sealed record PostDeleted(PostId PostId, UserId AuthorId) : IDomainEvent;
