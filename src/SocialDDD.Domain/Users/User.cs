using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    public Username Username { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public Handle Handle { get; private set; } = null!;
    public DisplayName DisplayName { get; private set; } = null!;
    public DateTime RegisteredAt { get; private set; }

    private User() { }

    public static User Register(
        Username username,
        Email email,
        PasswordHash passwordHash,
        Handle handle,
        DisplayName displayName)
    {
        var user = new User
        {
            Id = UserId.New(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Handle = handle,
            DisplayName = displayName,
            RegisteredAt = DateTime.UtcNow
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public void UpdateDisplayName(DisplayName newName) => DisplayName = newName;
}
