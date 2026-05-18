using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

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

        if (stored.Code != request.Code)
            throw new DomainException("Invalid verification code.");

        user.Activate();

        await userRepository.UpdateAsync(user, ct);
        await codeRepository.DeleteAsync(user.Id, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }
}
