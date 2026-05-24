using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Social.Posts.Events;

public sealed record PostDeleted(PostId PostId, UserId AuthorId) : IDomainEvent;
