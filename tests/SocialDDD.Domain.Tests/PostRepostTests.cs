using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Posts.Events;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostRepostTests
{
    private static UserId AnyAuthor() => UserId.New();
    private static Handle AnyHandle(string value = "alice") => new(value);

    private static Post AnyPost(UserId? authorId = null)
    {
        var post = Post.Create(authorId ?? AnyAuthor(), new PostContent("Hello world"));
        post.PopDomainEvents();
        return post;
    }

    [Fact]
    public void CreateRepost_SelfRepost_ThrowsDomainException()
    {
        var authorHandle = AnyHandle("alice");
        var authorId = AnyAuthor();
        var original = AnyPost(authorId: authorId);

        var act = () => Post.CreateRepost(original, authorHandle, authorId, authorHandle, null);

        act.Should().Throw<DomainException>().WithMessage("*Cannot repost your own post*");
    }

    [Fact]
    public void CreateRepost_CommentaryOver280Chars_ThrowsDomainValidationException()
    {
        var original = AnyPost();
        var originalAuthorHandle = AnyHandle("alice");
        var reposterUserId = AnyAuthor();
        var reposterHandle = AnyHandle("bob");
        var longCommentary = new string('x', 281);

        var act = () => Post.CreateRepost(original, originalAuthorHandle, reposterUserId, reposterHandle, longCommentary);

        act.Should().Throw<DomainValidationException>().WithMessage("*280*");
    }

    [Fact]
    public void CreateRepost_ValidArgs_SetsOriginalPostIdAndRaisesEvent()
    {
        var original = AnyPost();
        var originalAuthorHandle = AnyHandle("alice");
        var reposterUserId = AnyAuthor();
        var reposterHandle = AnyHandle("bob");

        var repost = Post.CreateRepost(original, originalAuthorHandle, reposterUserId, reposterHandle, "Great post!");

        repost.OriginalPostId.Should().Be(original.Id);
        repost.Content!.Value.Should().Be("Great post!");
        repost.AuthorId.Should().Be(reposterUserId);

        var events = repost.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<PostReposted>()
            .Which.OriginalPostId.Should().Be(original.Id);
    }

    [Fact]
    public void CreateRepost_DeletedOriginalPost_ThrowsDomainException()
    {
        var original = AnyPost();
        original.Delete();
        original.PopDomainEvents();
        var originalAuthorHandle = AnyHandle("alice");
        var reposterUserId = AnyAuthor();
        var reposterHandle = AnyHandle("bob");

        var act = () => Post.CreateRepost(original, originalAuthorHandle, reposterUserId, reposterHandle, null);

        act.Should().Throw<DomainException>().WithMessage("*deleted*");
    }

    [Fact]
    public void CreateRepost_NoCommentary_ContentIsNull()
    {
        var original = AnyPost();
        var originalAuthorHandle = AnyHandle("alice");
        var reposterUserId = AnyAuthor();
        var reposterHandle = AnyHandle("bob");

        var repost = Post.CreateRepost(original, originalAuthorHandle, reposterUserId, reposterHandle, null);

        repost.Content.Should().BeNull();
        repost.OriginalPostId.Should().Be(original.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRepost_BlankCommentary_ContentIsNull(string commentary)
    {
        var original = AnyPost();
        var originalAuthorHandle = AnyHandle("alice");
        var reposterUserId = AnyAuthor();
        var reposterHandle = AnyHandle("bob");

        var repost = Post.CreateRepost(original, originalAuthorHandle, reposterUserId, reposterHandle, commentary);

        repost.Content.Should().BeNull();
        repost.OriginalPostId.Should().Be(original.Id);
    }

    [Fact]
    public void CreateRepost_RepostOfRepost_ThrowsDomainException()
    {
        var originalAuthorHandle = AnyHandle("alice");
        var firstReposterUserId = AnyAuthor();
        var firstReposterHandle = AnyHandle("bob");
        var original = AnyPost();

        var repost = Post.CreateRepost(original, originalAuthorHandle, firstReposterUserId, firstReposterHandle, null);
        repost.PopDomainEvents();

        var secondReposterUserId = AnyAuthor();
        var secondReposterHandle = AnyHandle("charlie");

        var act = () => Post.CreateRepost(repost, firstReposterHandle, secondReposterUserId, secondReposterHandle, null);

        act.Should().Throw<DomainException>().WithMessage("*Cannot repost a repost*");
    }
}
