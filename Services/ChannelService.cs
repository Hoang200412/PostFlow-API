using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ChannelService : IChannelService
    {
        private readonly AppDbContext _context;

        public ChannelService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SocialAccount>> GetListAsync(string userId)
        {
            return await _context.SocialAccounts
                .Where(sa => sa.UserId == userId)
                .OrderBy(sa => sa.Platform)
                .ToListAsync();
        }

        public async Task<SocialAccount> ConnectAsync(string userId, ConnectChannelRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.ChannelIdOrPageId))
            {
                throw new BadRequestException("AccountName và ChannelIdOrPageId không được để trống.");
            }

            var exists = await _context.SocialAccounts
                .AnyAsync(sa => sa.UserId == userId && sa.Platform == request.Platform && sa.ChannelIdOrPageId == request.ChannelIdOrPageId);

            if (exists)
            {
                throw new ConflictException("Kênh này đã được kết nối trước đó.");
            }

            var channel = new SocialAccount
            {
                UserId = userId,
                Platform = request.Platform,
                AccountName = request.AccountName,
                ChannelIdOrPageId = request.ChannelIdOrPageId,
                AccessToken = Guid.NewGuid().ToString(),
                RefreshToken = Guid.NewGuid().ToString(),
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true
            };

            _context.SocialAccounts.Add(channel);
            await _context.SaveChangesAsync();

            return channel;
        }

        public async Task DisconnectAsync(string userId, string id)
        {
            var channel = await _context.SocialAccounts
                .FirstOrDefaultAsync(sa => sa.Id == id && sa.UserId == userId);

            if (channel == null)
            {
                throw new NotFoundException($"Không tìm thấy kênh social với ID {id}");
            }

            var hasActiveJobs = await _context.UploadJobs
                .AnyAsync(j => j.SocialAccountId == id && (j.Status == UploadStatus.Pending || j.Status == UploadStatus.Processing));

            if (hasActiveJobs)
            {
                throw new ConflictException("Không thể hủy kết nối kênh khi có bài đăng đang chờ hoặc đang tải lên.");
            }

            _context.SocialAccounts.Remove(channel);
            await _context.SaveChangesAsync();
        }
    }
}
