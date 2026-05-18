namespace SocialDDD.Domain.Users;

public interface IPasswordResetTokenRepository
{
    Task SaveAsync(UserId userId, PasswordResetToken token, CancellationToken ct = default);
    Task<(UserId UserId, PasswordResetToken Token)?> FindByTokenAsync(string token, CancellationToken ct = default);
    Task DeleteByUserIdAsync(UserId userId, CancellationToken ct = default);
}
