using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EmbeddronicsBackend.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(EmbeddronicsDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

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
        // Simple password hashing - in production, use BCrypt or similar
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "EmbeddronicsSalt"));
        return Convert.ToBase64String(hashedBytes);
    }
}