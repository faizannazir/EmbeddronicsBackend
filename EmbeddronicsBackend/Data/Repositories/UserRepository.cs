using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByEmailWithOrdersAsync(string email)
    {
        return await _dbSet
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetClientsByStatusAsync(string status)
    {
        return await _dbSet
            .Where(u => u.Role == "client" && u.Status == status)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(string role)
    {
        return await _dbSet
            .Where(u => u.Role == role)
            .ToListAsync();
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(u => u.Email == email);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
    }
}