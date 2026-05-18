using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Posts.Events;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostTests
{
    private static UserId AnyAuthor() => UserId.New();
    private static Handle AnyHandle(string value = "alice") => new(value);

    [Fact]
    public void Create_ValidArgs_CreatesPostAndRaisesEvent()
    {
        var authorId = AnyAuthor();
        var post = Post.Create(authorId, new PostContent("Hello world"));

        post.AuthorId.Should().Be(authorId);
        post.Content.Value.Should().Be("Hello world");
        post.IsDeleted.Should().BeFalse();

        var events = post.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<PostCreated>()
            .Which.AuthorId.Should().Be(authorId);
    }

    [Fact]
    public void Delete_ActivePost_SetsDeletedAndRaisesEvent()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        post.PopDomainEvents();

        post.Delete();

        post.IsDeleted.Should().BeTrue();
        post.PopDomainEvents().Should().ContainSingle()
            .Which.Should().BeOfType<PostDeleted>();
    }

    [Fact]
    public void Delete_AlreadyDeleted_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        post.Delete();

        var act = post.Delete;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ContentTooLong_ThrowsDomainException()
    {
        var act = () => Post.Create(AnyAuthor(), new PostContent(new string('x', 281)));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_EmptyContent_ThrowsDomainException()
    {
        var act = () => Post.Create(AnyAuthor(), new PostContent(""));

        act.Should().Throw<DomainException>();
    }

    // ---- Like tests ----

    [Fact]
    public void Like_FirstLike_AddsToLikedByAndRaisesEvent()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        post.PopDomainEvents();
        var handle = AnyHandle("bob");

        post.Like(handle);

        post.LikedBy.Should().Contain(handle);
        post.LikeCount.Should().Be(1);
        post.PopDomainEvents().Should().ContainSingle()
            .Which.Should().BeOfType<PostLiked>()
            .Which.LikedByHandle.Should().Be(handle);
    }

    [Fact]
    public void Like_SameUserLikesTwice_ThrowsAlreadyLikedException()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        var handle = AnyHandle("bob");
        post.Like(handle);

        var act = () => post.Like(handle);

        act.Should().Throw<AlreadyLikedException>();
    }

    [Fact]
    public void Unlike_AfterLike_RemovesFromLikedByAndRaisesEvent()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        var handle = AnyHandle("bob");
        post.Like(handle);
        post.PopDomainEvents();

        post.Unlike(handle);

        post.LikedBy.Should().NotContain(handle);
        post.LikeCount.Should().Be(0);
        post.PopDomainEvents().Should().ContainSingle()
            .Which.Should().BeOfType<PostUnliked>()
            .Which.UnlikedByHandle.Should().Be(handle);
    }

    [Fact]
    public void Unlike_WithoutPriorLike_ThrowsNotLikedException()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        var handle = AnyHandle("bob");

        var act = () => post.Unlike(handle);

        act.Should().Throw<NotLikedException>();
    }

    [Fact]
    public void LikeCount_ReflectsSetSize()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));

        post.Like(AnyHandle("alice"));
        post.Like(AnyHandle("bob"));
        post.Like(AnyHandle("charlie"));

        post.LikeCount.Should().Be(3);

        post.Unlike(AnyHandle("bob"));

        post.LikeCount.Should().Be(2);
    }

    [Fact]
    public void Like_DeletedPost_ThrowsDomainValidationException()
    {
        var post = Post.Create(AnyAuthor(), new PostContent("Hi"));
        post.Delete();

        var act = () => post.Like(AnyHandle("alice"));

        act.Should().Throw<DomainValidationException>();
    }
}
