namespace EmbeddronicsBackend.Services;

public interface IUserRegistrationService
{
    bool IsAdminRegistrationEnabled { get; }
    string GetDefaultRoleForNewUsers();
    string GetDefaultStatusForNewUsers();
    bool IsEmailAllowedForAdminRegistration(string email);
}