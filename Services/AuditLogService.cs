using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string action, string targetType, string? targetId, string? targetLabel, object? metadata = null)
        {
            try
            {
                var http = _httpContextAccessor.HttpContext;
                var user = http?.User;

                _context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    AdminId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system",
                    AdminUsername = user?.FindFirstValue(ClaimTypes.Name) ?? "system",
                    Action = action,
                    TargetType = targetType,
                    TargetId = targetId,
                    TargetLabel = targetLabel,
                    Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata),
                    IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Ghi log kiểm toán lỗi không được làm hỏng thao tác chính đã thành công
                _logger.LogError(ex, "Không thể ghi audit log cho hành động {Action}", action);
            }
        }
    }
}
