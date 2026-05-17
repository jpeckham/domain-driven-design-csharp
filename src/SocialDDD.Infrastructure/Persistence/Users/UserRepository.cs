using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Users;

internal sealed class UserRepository(MongoDbContext context) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken ct = default) =>
        context.Users.InsertOneAsync(user, cancellationToken: ct);

    public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Email == email)
            .FirstOrDefaultAsync(ct);

    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Username == username)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Email == email)
            .AnyAsync(ct);

    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Username == username)
            .AnyAsync(ct);

    public async Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users
            .Find(u => u.Id == id)
            .AnyAsync(ct);
}
