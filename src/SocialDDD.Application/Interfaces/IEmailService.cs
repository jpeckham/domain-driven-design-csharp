namespace SocialDDD.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default);
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
}
