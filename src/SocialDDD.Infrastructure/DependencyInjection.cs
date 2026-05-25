using SocialDDD.Domain.Social.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Social.Posts;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Domain.Identity.Users.Events;
using SocialDDD.Infrastructure.Identity.Auth;
using SocialDDD.Infrastructure.Identity.Emails;
using SocialDDD.Infrastructure.Events;
using SocialDDD.Infrastructure.Events.Handlers;
using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Infrastructure.Social.Persistence.Blocks;
using SocialDDD.Infrastructure.Social.Persistence.Follows;
using SocialDDD.Infrastructure.Identity.Persistence.OtpCodes;
using SocialDDD.Infrastructure.Identity.Persistence.PasswordResetTokens;
using SocialDDD.Infrastructure.Social.Persistence.Posts;
using SocialDDD.Infrastructure.Identity.Persistence.RememberedDevices;
using SocialDDD.Infrastructure.Identity.Persistence.Users;
using SocialDDD.Infrastructure.Identity.Persistence.VerificationCodes;
using SocialDDD.Infrastructure.Social.PostMediaStorage;
using SocialDDD.Infrastructure.Social.ProfileImages;

namespace SocialDDD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoSettings>(configuration.GetSection("Mongo"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<AcsEmailOptions>(configuration.GetSection(AcsEmailOptions.SectionName));

        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IBlockRepository, MongoDbBlockRepository>();
        services.AddScoped<IFollowRepository, MongoDbFollowRepository>();

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
        services.AddScoped<IDomainEventHandler<UserRegistered>, SendUserRegisteredVerificationEmailHandler>();
        services.AddScoped<IDomainEventHandler<UserVerificationRequested>, SendUserVerificationRequestedEmailHandler>();
        services.AddScoped<IDomainEventHandler<PasswordResetRequested>, SendPasswordResetRequestedEmailHandler>();
        services.AddScoped<IDomainEventHandler<LoginChallenged>, SendLoginChallengedEmailHandler>();

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
