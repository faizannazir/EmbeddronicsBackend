namespace EmbeddronicsBackend.Services
{
    public interface ITokenBlacklistService
    {
        Task BlacklistTokenAsync(string jti, DateTime expiration);
        Task<bool> IsTokenBlacklistedAsync(string jti);
        Task CleanupExpiredTokensAsync();
    }
}