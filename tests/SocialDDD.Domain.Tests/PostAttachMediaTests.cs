using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostAttachMediaTests
{
    private static UserId AnyAuthor() => UserId.New();
    private static PostContent AnyContent() => new("Hello world");

    private static PostMedia MakeImage(int sort = 0) => new(
        Guid.NewGuid(), MediaKind.Image, "key", "image/jpeg", 1024,
        800, 600, null, null, null, sort);

    private static PostMedia MakeVideo(int sort = 0) => new(
        Guid.NewGuid(), MediaKind.Video, "key", "video/mp4", 5_000_000,
        1920, 1080, 30_000, null, null, sort);

    [Fact]
    public void Create_WithNoMedia_MediaIsEmpty()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        post.Media.Should().BeEmpty();
    }

    [Fact]
    public void AttachMedia_FourImages_Succeeds()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 4).Select(i => MakeImage(i)).ToList();

        var act = () => post.AttachMedia(media);

        act.Should().NotThrow();
        post.Media.Should().HaveCount(4);
    }

    [Fact]
    public void AttachMedia_OneVideo_Succeeds()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());

        var act = () => post.AttachMedia([MakeVideo()]);

        act.Should().NotThrow();
        post.Media.Should().HaveCount(1);
    }

    [Fact]
    public void AttachMedia_FiveImages_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 5).Select(i => MakeImage(i)).ToList();

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*4*");
    }

    [Fact]
    public void AttachMedia_MixedKinds_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = new List<PostMedia> { MakeImage(), MakeVideo() };

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*same kind*");
    }

    [Fact]
    public void AttachMedia_TwoVideos_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = new List<PostMedia> { MakeVideo(), MakeVideo() };

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*1 video*");
    }

    [Fact]
    public void AttachMedia_AssignsSortOrderByPosition()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 3).Select(_ => MakeImage(99)).ToList();

        post.AttachMedia(media);

        post.Media.Select(m => m.SortOrder).Should().Equal(0, 1, 2);
    }
}
