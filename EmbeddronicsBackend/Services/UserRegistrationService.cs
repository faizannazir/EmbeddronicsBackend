namespace EmbeddronicsBackend.Services;

public class UserRegistrationService : IUserRegistrationService
{
    private readonly IConfiguration _configuration;
    
    // Predefined admin emails - no new admin registrations allowed
    private readonly HashSet<string> _allowedAdminEmails = new()
    {
        "faizannazir289@gmail.com",
        "info@embeddronics.com", 
        "zeeshannazeer1998@gmail.com",
        "nomimalik15@gmail.com"
    };

    public UserRegistrationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsAdminRegistrationEnabled => false; // Disabled as per requirements

    public string GetDefaultRoleForNewUsers() => "client";

    public string GetDefaultStatusForNewUsers() => "pending";

    public bool IsEmailAllowedForAdminRegistration(string email)
    {
        return _allowedAdminEmails.Contains(email.ToLowerInvariant());
    }
}