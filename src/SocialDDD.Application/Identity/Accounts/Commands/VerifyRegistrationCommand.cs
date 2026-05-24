using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Identity.Accounts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Application.Identity.Accounts.Commands;

public sealed class VerifyRegistrationCommand(
    IUserRepository userRepository,
    IVerificationCodeRepository codeRepository,
    ITokenService tokenService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<TokenResponse> ExecuteAsync(VerifyRegistrationRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);

        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("No pending registration found for that email.");

        var stored = await codeRepository.FindByUserIdAsync(user.Id, ct)
            ?? throw new DomainException("No verification code found. Please register again.");

        if (stored.IsExpired(DateTimeOffset.UtcNow))
            throw new DomainException("Verification code has expired. Please register again.");

        var storedBytes = System.Text.Encoding.UTF8.GetBytes(stored.Code);
        var providedBytes = System.Text.Encoding.UTF8.GetBytes(request.Code);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes))
            throw new DomainException("Invalid verification code.");

        if (user.Status == UserStatus.Active)
            throw new DomainValidationException("Account is already verified.");

        user.Activate();

        await userRepository.UpdateAsync(user, ct);
        await codeRepository.DeleteAsync(user.Id, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }
}
