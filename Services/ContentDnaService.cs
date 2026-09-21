using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ContentDnaService : IContentDnaService
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;

        public ContentDnaService(AppDbContext context, IStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        public async Task<IEnumerable<ContentDnaDto>> GetListAsync(string userId)
        {
            var list = await _context.ContentDNAs
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return list.Select(c => c.ToDto());
        }

        public async Task<ContentDnaDto> CreateAsync(string userId, ContentDnaCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BrandName))
            {
                throw new BadRequestException("Brand name cannot be empty.");
            }

            var supportedVoices = new[] { "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural", "en-US-JennyNeural", "en-US-GuyNeural" };
            if (!supportedVoices.Contains(dto.VoiceProfile))
            {
                throw new BadRequestException("Giọng đọc không được hỗ trợ.");
            }

            var dna = new ContentDNA
            {
                UserId = userId,
                BrandName = dto.BrandName,
                VoiceProfile = dto.VoiceProfile,
                AvatarAssetPath = dto.AvatarAssetPath ?? string.Empty,
                ScriptStructure = dto.ScriptStructure ?? "Hook/Context/Mechanism/Twist"
            };

            _context.ContentDNAs.Add(dna);
            await _context.SaveChangesAsync();

            return dna.ToDto();
        }

        public async Task<ContentDnaDto> UpdateAsync(string userId, string id, ContentDnaCreateDto dto)
        {
            var dna = await _context.ContentDNAs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (dna == null)
            {
                throw new NotFoundException($"Không tìm thấy Content DNA với ID {id}");
            }

            if (string.IsNullOrWhiteSpace(dto.BrandName))
            {
                throw new BadRequestException("Brand name cannot be empty.");
            }

            var supportedVoices = new[] { "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural", "en-US-JennyNeural", "en-US-GuyNeural" };
            if (!supportedVoices.Contains(dto.VoiceProfile))
            {
                throw new BadRequestException("Giọng đọc không được hỗ trợ.");
            }

            dna.BrandName = dto.BrandName;
            dna.VoiceProfile = dto.VoiceProfile;
            dna.AvatarAssetPath = dto.AvatarAssetPath ?? dna.AvatarAssetPath;
            dna.ScriptStructure = dto.ScriptStructure ?? dna.ScriptStructure;

            await _context.SaveChangesAsync();
            return dna.ToDto();
        }

        public async Task DeleteAsync(string userId, string id)
        {
            var dna = await _context.ContentDNAs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (dna == null)
            {
                throw new NotFoundException($"Không tìm thấy Content DNA với ID {id}");
            }

            var isReferenced = await _context.VideoAssets.AnyAsync(v => v.ContentDNAId == id);
            if (isReferenced)
            {
                throw new ConflictException("Không thể xóa Content DNA này vì đang có video liên kết sử dụng.");
            }

            _context.ContentDNAs.Remove(dna);
            await _context.SaveChangesAsync();
        }

        public async Task<string> UploadAvatarAsync(string userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("Vui lòng chọn file avatar hợp lệ.");
            }

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".png" && ext != ".gif")
            {
                throw new BadRequestException("Chỉ hỗ trợ định dạng PNG và GIF.");
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            using var stream = file.OpenReadStream();
            var relativePath = await _storageService.SaveFileAsync(stream, $"avatars/{userId}", fileName, file.ContentType);

            return relativePath;
        }
    }
}
