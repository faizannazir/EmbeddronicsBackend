using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailWithOrdersAsync(string email);
    Task<IEnumerable<User>> GetClientsByStatusAsync(string status);
    Task<IEnumerable<User>> GetUsersByRoleAsync(string role);
    Task<bool> EmailExistsAsync(string email);
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
}