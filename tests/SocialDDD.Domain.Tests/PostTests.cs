using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Posts.Events;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostTests
{
    private static UserId AnyAuthor() => UserId.New();

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
}
