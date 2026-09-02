using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AdminService(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<IEnumerable<object>> GetUsersAsync()
        {
            var list = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return list.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            });
        }

        public async Task<object> CreateUserAsync(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new BadRequestException("Username, Email, and Password are required.");
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            {
                throw new ConflictException("Username already exists.");
            }

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                throw new ConflictException("Email already exists.");
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

            return new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            };
        }

        public async Task<object> GetUsageReportAsync(DateTime? date)
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

            return new
            {
                date = targetDate,
                summary,
                details = usageLogs.Select(l => new
                {
                    l.Id,
                    l.Provider,
                    l.CallCount,
                    l.CostEstimate,
                    User = l.User == null ? null : new { l.User.Id, l.User.Username }
                })
            };
        }

        public async Task<SystemSetting> UpdateSettingAsync(string key, UpdateSettingRequest request)
        {
            if (request.Value == null)
            {
                throw new BadRequestException("Value cannot be null.");
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
            return setting;
        }
    }
}
