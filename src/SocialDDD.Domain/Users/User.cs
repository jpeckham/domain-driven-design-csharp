using SocialDDD.Domain.Exceptions;
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
    public UserStatus Status { get; private set; }

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
            RegisteredAt = DateTime.UtcNow,
            Status = UserStatus.Pending
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public static User RegisterImmediate(
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
            RegisteredAt = DateTime.UtcNow,
            Status = UserStatus.Active
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public void Activate()
    {
        if (Status == UserStatus.Active)
            throw new DomainValidationException("User is already active.");
        Status = UserStatus.Active;
        RaiseDomainEvent(new UserActivated(Id));
    }

    public void UpdateDisplayName(DisplayName newName) => DisplayName = newName;

    public void ResetPassword(PasswordHash newHash)
    {
        PasswordHash = newHash;
        RaiseDomainEvent(new Events.PasswordReset(Id));
    }
}
