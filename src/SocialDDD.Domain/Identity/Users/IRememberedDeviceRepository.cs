namespace SocialDDD.Domain.Identity.Users;

public interface IRememberedDeviceRepository
{
    Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
    Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
}
