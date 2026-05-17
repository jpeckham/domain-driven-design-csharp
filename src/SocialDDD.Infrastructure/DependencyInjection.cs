using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Auth;
using SocialDDD.Infrastructure.Events;
using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Infrastructure.Persistence.Posts;
using SocialDDD.Infrastructure.Persistence.Users;

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

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
