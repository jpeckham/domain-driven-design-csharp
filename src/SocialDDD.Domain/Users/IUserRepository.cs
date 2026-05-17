namespace SocialDDD.Domain.Users;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default);
    Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default);
    Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default);
    Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
