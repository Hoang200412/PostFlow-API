using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VideoCreationSessionService : IVideoCreationSessionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<VideoCreationSessionService> _logger;

        public VideoCreationSessionService(AppDbContext context, ILogger<VideoCreationSessionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CreationSessionDto?> GetSessionAsync(string userId)
        {
            var session = await _context.VideoCreationSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (session == null) return null;

            return session.ToDto();
        }

        public async Task<CreationSessionDto> UpsertSessionAsync(string userId, UpsertCreationSessionRequest request)
        {
            var session = await _context.VideoCreationSessions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (session == null)
            {
                session = new VideoCreationSession
                {
                    UserId = userId,
                    Step = request.Step,
                    IdeaPrompt = request.IdeaPrompt ?? string.Empty,
                    SelectedVoice = string.IsNullOrWhiteSpace(request.SelectedVoice) ? "vi-VN-NamMinhNeural" : request.SelectedVoice,
                    SelectedStructure = string.IsNullOrWhiteSpace(request.SelectedStructure) ? "str_short" : request.SelectedStructure,
                    SelectedTone = string.IsNullOrWhiteSpace(request.SelectedTone) ? "Năng động, cuốn hút, bắt trend" : request.SelectedTone,
                    SelectedDuration = string.IsNullOrWhiteSpace(request.SelectedDuration) ? "30–45 giây" : request.SelectedDuration,
                    ScriptJson = request.ScriptJson,
                    CreatedVideoId = request.CreatedVideoId,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.VideoCreationSessions.Add(session);
            }
            else
            {
                session.Step = request.Step;
                session.IdeaPrompt = request.IdeaPrompt ?? session.IdeaPrompt;
                if (!string.IsNullOrWhiteSpace(request.SelectedVoice)) session.SelectedVoice = request.SelectedVoice;
                if (!string.IsNullOrWhiteSpace(request.SelectedStructure)) session.SelectedStructure = request.SelectedStructure;
                if (!string.IsNullOrWhiteSpace(request.SelectedTone)) session.SelectedTone = request.SelectedTone;
                if (!string.IsNullOrWhiteSpace(request.SelectedDuration)) session.SelectedDuration = request.SelectedDuration;
                if (request.ScriptJson != null) session.ScriptJson = request.ScriptJson;
                if (request.CreatedVideoId != null) session.CreatedVideoId = request.CreatedVideoId;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Đã lưu VideoCreationSession cho User {UserId}, Step {Step}", userId, session.Step);

            return session.ToDto();
        }

        public async Task DeleteSessionAsync(string userId)
        {
            var session = await _context.VideoCreationSessions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (session != null)
            {
                _context.VideoCreationSessions.Remove(session);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã xóa VideoCreationSession cho User {UserId}", userId);
            }
        }
    }
}
