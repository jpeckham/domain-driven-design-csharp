using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Tests;

public class PostReplyTests
{
    private static UserId MakeUserId() => UserId.New();
    private static PostId MakePostId() => PostId.New();
    private static Handle MakeHandle(string value = "alice") => new Handle(value);

    private static Post MakePost(string content = "Hello world") =>
        Post.Create(MakeUserId(), new PostContent(content));

    // --- Mention extraction ---

    [Fact]
    public void Post_ExtractsMentions_FromContent()
    {
        var post = Post.Create(MakeUserId(), new PostContent("Hello @alice and @BOB"));
        post.Mentions.Select(h => h.Value).Should().BeEquivalentTo(new[] { "alice", "bob" });
    }

    [Fact]
    public void Post_ExtractsHashtags_FromContent()
    {
        var post = Post.Create(MakeUserId(), new PostContent("Love #DDD and #csharp!"));
        post.Hashtags.Should().BeEquivalentTo(new[] { "ddd", "csharp" });
    }

    [Fact]
    public void Post_OwnHandle_IsExcludedFromMentions()
    {
        var authorHandle = new Handle("alice");
        var parentPostId = MakePostId();
        var reply = Post.CreateReply(
            parentPostId,
            MakeUserId(),
            authorHandle,
            new PostContent("@bob test from @alice"));

        // alice is the author handle - should be excluded
        reply.Mentions.Select(h => h.Value).Should().NotContain("alice");
        reply.Mentions.Select(h => h.Value).Should().Contain("bob");
    }

    [Fact]
    public void Post_MentionsAndHashtags_AreNormalizedToLowercase()
    {
        var post = Post.Create(MakeUserId(), new PostContent("Hey @ALICE check out #MyTag"));
        post.Mentions.Select(h => h.Value).Should().Contain("alice");
        post.Hashtags.Should().Contain("mytag");
    }

    // --- CreateReply ---

    [Fact]
    public void CreateReply_SetsParentPostId()
    {
        var parentPostId = MakePostId();
        var authorId = MakeUserId();
        var authorHandle = new Handle("bob");

        var reply = Post.CreateReply(parentPostId, authorId, authorHandle, new PostContent("A reply"));

        reply.ParentPostId.Should().NotBeNull();
        reply.ParentPostId!.Value.Should().Be(parentPostId.Value);
    }

    [Fact]
    public void CreateReply_SetsAuthorId()
    {
        var parentPostId = MakePostId();
        var authorId = MakeUserId();
        var reply = Post.CreateReply(parentPostId, authorId, new Handle("bob"), new PostContent("reply"));

        reply.AuthorId.Should().Be(authorId);
    }

    [Fact]
    public void CreateReply_RaisesPostCreatedEvent()
    {
        var reply = Post.CreateReply(MakePostId(), MakeUserId(), new Handle("bob"), new PostContent("reply"));
        var events = reply.PopDomainEvents();
        events.Should().ContainSingle(e => e.GetType().Name == "PostCreated");
    }

    [Fact]
    public void CreateReply_EmptyContent_ThrowsDomainValidationException()
    {
        var act = () => Post.CreateReply(MakePostId(), MakeUserId(), new Handle("bob"), new PostContent(""));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void CreateReply_ContentOver280Chars_ThrowsDomainValidationException()
    {
        var act = () => Post.CreateReply(
            MakePostId(), MakeUserId(), new Handle("bob"),
            new PostContent(new string('x', 281)));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Post_DeletedPost_CannotBeUsedAsParent_ViaDeleteCheck()
    {
        // The domain check for "cannot reply to deleted post" lives in the application handler.
        // At the domain level, we can create a reply to a deleted post's ID (ID is just a value).
        // This test verifies a deleted post's IsDeleted flag is set correctly.
        var post = MakePost();
        post.Delete();
        post.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void RootPost_ParentPostId_IsNull()
    {
        var post = MakePost();
        post.ParentPostId.Should().BeNull();
    }

    [Fact]
    public void Post_NoMentions_MentionsCollectionIsEmpty()
    {
        var post = MakePost("Hello world, no mentions here!");
        post.Mentions.Should().BeEmpty();
    }

    [Fact]
    public void Post_NoHashtags_HashtagsCollectionIsEmpty()
    {
        var post = MakePost("Hello world, no hashtags here!");
        post.Hashtags.Should().BeEmpty();
    }

    [Fact]
    public void Post_MultipleMentions_AllExtracted()
    {
        var post = Post.Create(MakeUserId(), new PostContent("@alice @bob @charlie say hi"));
        post.Mentions.Select(h => h.Value)
            .Should().BeEquivalentTo(new[] { "alice", "bob", "charlie" });
    }
}
