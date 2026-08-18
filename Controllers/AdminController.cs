using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;

namespace PostFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AdminController(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var list = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return Ok(list.Select(u => new {
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            }));
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username, Email, and Password are required." });
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            {
                return Conflict(new { message = "Username already exists." });
            }

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                return Conflict(new { message = "Email already exists." });
            }

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Role = request.Role ?? "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Created($"/api/admin/users/{user.Id}", new {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            });
        }

        [HttpGet("usage")]
        public async Task<IActionResult> GetUsageReport([FromQuery] DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;

            var usageLogs = await _context.ApiUsageLogs
                .Include(l => l.User)
                .Where(l => l.Date == targetDate)
                .ToListAsync();

            var summary = usageLogs
                .GroupBy(l => l.Provider)
                .Select(g => new
                {
                    Provider = g.Key,
                    TotalCalls = g.Sum(x => x.CallCount),
                    TotalCost = g.Sum(x => x.CostEstimate)
                });

            return Ok(new
            {
                date = targetDate,
                summary,
                details = usageLogs.Select(l => new {
                    l.Id,
                    l.Provider,
                    l.CallCount,
                    l.CostEstimate,
                    User = l.User == null ? null : new { l.User.Id, l.User.Username }
                })
            });
        }

        [HttpPut("settings/{key}")]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
        {
            if (request.Value == null)
            {
                return BadRequest(new { message = "Value cannot be null." });
            }

            var setting = await _context.SystemSettings.FindAsync(key);
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = key,
                    Value = request.Value,
                    Description = request.Description ?? string.Empty
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = request.Value;
                if (request.Description != null)
                {
                    setting.Description = request.Description;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(setting);
        }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class UpdateSettingRequest
    {
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
