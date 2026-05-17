using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts.Events;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts;

public sealed class Post : AggregateRoot<PostId>
{
    public UserId AuthorId { get; private set; } = null!;
    public PostContent Content { get; private set; } = null!;
    public DateTime PostedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Post() { }

    public static Post Create(UserId authorId, PostContent content)
    {
        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = authorId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        post.RaiseDomainEvent(new PostCreated(post.Id, authorId));
        return post;
    }

    public void Delete()
    {
        if (IsDeleted)
            throw new DomainException("Post is already deleted.");

        IsDeleted = true;
        RaiseDomainEvent(new PostDeleted(Id, AuthorId));
    }
}
