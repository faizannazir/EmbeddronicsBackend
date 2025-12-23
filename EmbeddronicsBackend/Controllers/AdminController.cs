using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Services;

namespace EmbeddronicsBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly EmbeddronicsDbContext _context;
    private readonly IUserRegistrationService _registrationService;

    public AdminController(EmbeddronicsDbContext context, IUserRegistrationService registrationService)
    {
        _context = context;
        _registrationService = registrationService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Name,
                u.Role,
                u.Status,
                u.Company,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = users, message = "Users retrieved successfully" });
    }

    [HttpGet("registration-settings")]
    public IActionResult GetRegistrationSettings()
    {
        var settings = new
        {
            IsAdminRegistrationEnabled = _registrationService.IsAdminRegistrationEnabled,
            DefaultRole = _registrationService.GetDefaultRoleForNewUsers(),
            DefaultStatus = _registrationService.GetDefaultStatusForNewUsers()
        };

        return Ok(new { success = true, data = settings, message = "Registration settings retrieved successfully" });
    }

    [HttpGet("test-seeding")]
    public async Task<IActionResult> TestSeeding()
    {
        var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
        var adminEmails = await _context.Users
            .Where(u => u.Role == "admin")
            .Select(u => u.Email)
            .ToListAsync();

        var result = new
        {
            AdminCount = adminCount,
            AdminEmails = adminEmails,
            SeedingStatus = adminCount > 0 ? "Success" : "Failed"
        };

        return Ok(new { success = true, data = result, message = "Seeding test completed" });
    }
}