namespace SocialDDD.Domain.Identity.Users;

public interface IVerificationCodeRepository
{
    Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default);
    Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task DeleteAsync(UserId userId, CancellationToken ct = default);
}
