namespace SocialDDD.Domain.Users;

public interface IOtpRepository
{
    Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default);
    Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
    Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
}
