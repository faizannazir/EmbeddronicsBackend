using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace EmbeddronicsBackend.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(EmbeddronicsDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if we need to migrate existing admin users from SHA256 to BCrypt
        await MigrateExistingAdminPasswordsAsync(context);

        // Check if admin users already exist
        if (await context.Users.AnyAsync(u => u.Role == "admin"))
        {
            return; // Admin users already seeded
        }

        // Admin emails to seed
        var adminEmails = new[]
        {
            "faizannazir289@gmail.com",
            "info@embeddronics.com",
            "zeeshannazeer1998@gmail.com",
            "nomimalik15@gmail.com"
        };

        var adminUsers = new List<User>();

        foreach (var email in adminEmails)
        {
            var adminUser = new User
            {
                Email = email,
                Name = GetNameFromEmail(email),
                PasswordHash = HashPassword("Admin@123"), // Default password for admin accounts
                Role = "admin",
                Status = "active",
                Company = "Embeddronics",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            adminUsers.Add(adminUser);
        }

        // Add admin users to database
        await context.Users.AddRangeAsync(adminUsers);
        await context.SaveChangesAsync();
    }

    private static async Task MigrateExistingAdminPasswordsAsync(EmbeddronicsDbContext context)
    {
        // Get all admin users that might have SHA256 hashed passwords
        var adminUsers = await context.Users
            .Where(u => u.Role == "admin")
            .ToListAsync();

        var defaultPassword = "Admin@123";
        var legacySha256Hash1 = LegacyHashPassword(defaultPassword, "EmbeddronicsSalt");
        var legacySha256Hash2 = LegacyHashPassword(defaultPassword, "EmbeddronicsSalt2024");

        bool hasChanges = false;

        foreach (var user in adminUsers)
        {
            // Check if this user has a legacy SHA256 hash
            if (user.PasswordHash == legacySha256Hash1 || user.PasswordHash == legacySha256Hash2)
            {
                // Migrate to BCrypt
                user.PasswordHash = HashPassword(defaultPassword);
                user.UpdatedAt = DateTime.UtcNow;
                hasChanges = true;
                
                Console.WriteLine($"Migrated password for admin user: {user.Email}");
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            Console.WriteLine("Admin password migration completed.");
        }
    }

    private static string GetNameFromEmail(string email)
    {
        return email switch
        {
            "faizannazir289@gmail.com" => "Faizan Nazir",
            "info@embeddronics.com" => "Embeddronics Admin",
            "zeeshannazeer1998@gmail.com" => "Zeeshan Nazeer",
            "nomimalik15@gmail.com" => "Nomi Malik",
            _ => email.Split('@')[0].Replace(".", " ").Replace("_", " ")
        };
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    private static string LegacyHashPassword(string password, string salt)
    {
        // Legacy SHA256 hashing method for migration purposes only
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + salt));
        return Convert.ToBase64String(hashedBytes);
    }
}