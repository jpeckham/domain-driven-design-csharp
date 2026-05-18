using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Auth;
using SocialDDD.Infrastructure.Emails;
using SocialDDD.Infrastructure.Events;
using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Infrastructure.Persistence.OtpCodes;
using SocialDDD.Infrastructure.Persistence.PasswordResetTokens;
using SocialDDD.Infrastructure.Persistence.Posts;
using SocialDDD.Infrastructure.Persistence.RememberedDevices;
using SocialDDD.Infrastructure.Persistence.Users;
using SocialDDD.Infrastructure.Persistence.VerificationCodes;
using SocialDDD.Infrastructure.PostMedia;
using SocialDDD.Infrastructure.ProfileImages;

namespace SocialDDD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoSettings>(configuration.GetSection("Mongo"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPostRepository, PostRepository>();

        // Verification code repository: "MongoDb" or "InMemory" (default)
        var verificationCodeRepo = configuration["Features:EmailVerificationRepository"] ?? "InMemory";
        if (verificationCodeRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IVerificationCodeRepository, MongoDbVerificationCodeRepository>();
        else
            services.AddSingleton<IVerificationCodeRepository, InMemoryVerificationCodeRepository>();

        // Remembered device repository: "MongoDb" or "InMemory" (default)
        var rememberedDeviceRepo = configuration["Features:RememberedDeviceRepository"] ?? "InMemory";
        if (rememberedDeviceRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IRememberedDeviceRepository, MongoDbRememberedDeviceRepository>();
        else
            services.AddSingleton<IRememberedDeviceRepository, InMemoryRememberedDeviceRepository>();

        // OTP repository: "MongoDb" or "InMemory" (default)
        var otpRepo = configuration["Features:OtpRepository"] ?? "InMemory";
        if (otpRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IOtpRepository, MongoDbOtpRepository>();
        else
            services.AddSingleton<IOtpRepository, InMemoryOtpRepository>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Email service: "AzureCommunication" or "Console" (default)
        var emailService = configuration["Features:EmailService"] ?? "Console";
        if (emailService.Equals("AzureCommunication", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailService, AzureCommunicationEmailService>();
        else
            services.AddScoped<IEmailService, ConsoleEmailService>();

        // Password reset token repository: "MongoDb" or "InMemory" (default)
        var passwordResetRepo = configuration["Features:PasswordResetTokenRepository"] ?? "InMemory";
        if (passwordResetRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IPasswordResetTokenRepository, MongoDbPasswordResetTokenRepository>();
        else
            services.AddSingleton<IPasswordResetTokenRepository, InMemoryPasswordResetTokenRepository>();

        // Profile image storage: local file system
        var profileImageDir = configuration["ProfileImages:Directory"] ?? "./data/profile-images";
        Directory.CreateDirectory(profileImageDir);
        services.AddSingleton<IProfileImageStorageService>(_ =>
            new LocalFileProfileImageStorageService(profileImageDir));

        // Post media storage: local file system
        var postMediaDir = configuration["PostMedia:Directory"] ?? "./data/post-media";
        Directory.CreateDirectory(postMediaDir);
        services.AddSingleton<IPostMediaStorageService>(_ =>
            new LocalFilePostMediaStorageService(postMediaDir));

        // Pending media store: singleton in-memory (1-hour TTL)
        services.AddSingleton<IPendingMediaStore, InMemoryPendingMediaStore>();

        services.AddScoped<RegisterPendingUserCommand>();
        services.AddScoped<VerifyRegistrationCommand>();
        services.AddScoped<LoginWithDeviceCommand>();
        services.AddScoped<VerifyDeviceOtpCommand>();
        services.AddScoped<RequestPasswordResetCommand>();
        services.AddScoped<ResetPasswordCommand>();

        return services;
    }
}
