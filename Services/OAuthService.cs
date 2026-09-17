using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class OAuthService : IOAuthService
    {
        private readonly AppDbContext _context;
        private readonly PlatformSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OAuthService> _logger;

        public OAuthService(
            AppDbContext context,
            IOptions<PlatformSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<OAuthService> logger)
        {
            _context = context;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string GenerateState(string userId, PlatformType platform, string? returnUrl = null)
        {
            var payload = new
            {
                UserId = userId,
                Platform = (int)platform,
                ReturnUrl = returnUrl ?? string.Empty,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public (string UserId, PlatformType? Platform, string ReturnUrl) DecodeState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                throw new BadRequestException("State không hợp lệ hoặc bị thiếu.");
            }

            try
            {
                string incoming = state.Replace('-', '+').Replace('_', '/');
                switch (incoming.Length % 4)
                {
                    case 2: incoming += "=="; break;
                    case 3: incoming += "="; break;
                }
                var bytes = Convert.FromBase64String(incoming);
                var json = Encoding.UTF8.GetString(bytes);
                using var doc = JsonDocument.Parse(json);
                var userId = doc.RootElement.GetProperty("UserId").GetString() ?? "";
                var returnUrl = doc.RootElement.TryGetProperty("ReturnUrl", out var rElem) ? rElem.GetString() ?? "" : "";
                PlatformType? platform = null;
                if (doc.RootElement.TryGetProperty("Platform", out var pElem))
                {
                    if (pElem.ValueKind == JsonValueKind.Number)
                    {
                        platform = (PlatformType)pElem.GetInt32();
                    }
                    else if (pElem.ValueKind == JsonValueKind.String && Enum.TryParse<PlatformType>(pElem.GetString(), true, out var parsed))
                    {
                        platform = parsed;
                    }
                }
                return (userId, platform, returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không thể giải mã OAuth state: {Msg}. Fallback sử dụng chuỗi state thô làm UserId.", ex.Message);
                return (state, null, "");
            }
        }

        public string GetAuthorizationUrl(PlatformType platform, string redirectUri, string state)
        {
            var encodedRedirect = Uri.EscapeDataString(redirectUri);
            var encodedState = Uri.EscapeDataString(state);

            switch (platform)
            {
                case PlatformType.YouTube:
                    var ytClientId = _settings.YouTube.ClientId;
                    var ytScopes = Uri.EscapeDataString("https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly openid email profile");
                    return $"https://accounts.google.com/o/oauth2/v2/auth?client_id={ytClientId}&redirect_uri={encodedRedirect}&response_type=code&scope={ytScopes}&access_type=offline&prompt=consent&state={encodedState}";

                case PlatformType.TikTok:
                    var ttClientKey = _settings.TikTok.ClientKey;
                    var ttScopes = Uri.EscapeDataString("user.info.basic,video.upload,video.publish");
                    return $"https://www.tiktok.com/v2/auth/authorize/?client_key={ttClientKey}&scope={ttScopes}&response_type=code&redirect_uri={encodedRedirect}&state={encodedState}";

                case PlatformType.FacebookReels:
                    var fbAppId = _settings.FacebookReels.AppId;
                    var fbScopes = Uri.EscapeDataString("pages_show_list,pages_read_engagement,pages_manage_posts,publish_video");
                    return $"https://www.facebook.com/v19.0/dialog/oauth?client_id={fbAppId}&redirect_uri={encodedRedirect}&scope={fbScopes}&state={encodedState}&response_type=code";

                default:
                    throw new BadRequestException($"Nền tảng {platform} không hỗ trợ đăng nhập OAuth.");
            }
        }

        public async Task<SocialAccountDto> HandleCallbackAsync(string userId, PlatformType platform, string code, string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BadRequestException("Authorization code không hợp lệ.");
            }

            var client = _httpClientFactory.CreateClient();

            string accessToken = string.Empty;
            string refreshToken = string.Empty;
            DateTime? tokenExpiresAt = null;
            string accountName = string.Empty;
            string channelOrPageId = string.Empty;

            switch (platform)
            {
                case PlatformType.YouTube:
                    {
                        var tokenRequestParams = new Dictionary<string, string>
                        {
                            { "code", code },
                            { "client_id", _settings.YouTube.ClientId },
                            { "client_secret", _settings.YouTube.ClientSecret },
                            { "redirect_uri", redirectUri },
                            { "grant_type", "authorization_code" }
                        };

                        var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequestParams));
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("Lỗi trao đổi token Google YouTube: {Resp}", responseBody);
                            throw new BadRequestException($"Không thể xác thực với Google: {responseBody}");
                        }

                        using var doc = JsonDocument.Parse(responseBody);
                        accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                        if (doc.RootElement.TryGetProperty("refresh_token", out var rtElem))
                        {
                            refreshToken = rtElem.GetString() ?? "";
                        }
                        if (doc.RootElement.TryGetProperty("expires_in", out var expElem))
                        {
                            tokenExpiresAt = DateTime.UtcNow.AddSeconds(expElem.GetInt32());
                        }

                        // Lấy thông tin kênh YouTube của người dùng
                        using var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");
                        profileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        var profileResp = await client.SendAsync(profileReq);

                        if (profileResp.IsSuccessStatusCode)
                        {
                            var profileBody = await profileResp.Content.ReadAsStringAsync();
                            using var profileDoc = JsonDocument.Parse(profileBody);
                            var items = profileDoc.RootElement.GetProperty("items");
                            if (items.GetArrayLength() > 0)
                            {
                                var firstItem = items[0];
                                channelOrPageId = firstItem.GetProperty("id").GetString() ?? "default_channel";
                                accountName = firstItem.GetProperty("snippet").GetProperty("title").GetString() ?? "Kênh YouTube";
                            }
                        }

                        if (string.IsNullOrEmpty(accountName))
                        {
                            accountName = "YouTube Channel (" + DateTime.UtcNow.ToString("dd/MM") + ")";
                            channelOrPageId = "yt_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        }
                        break;
                    }

                case PlatformType.TikTok:
                    {
                        var tokenRequestParams = new Dictionary<string, string>
                        {
                            { "client_key", _settings.TikTok.ClientKey },
                            { "client_secret", _settings.TikTok.ClientSecret },
                            { "code", code },
                            { "grant_type", "authorization_code" },
                            { "redirect_uri", redirectUri }
                        };

                        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/token/", new FormUrlEncodedContent(tokenRequestParams));
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("Lỗi trao đổi token TikTok: {Resp}", responseBody);
                            throw new BadRequestException($"Không thể xác thực với TikTok: {responseBody}");
                        }

                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("data", out var dataElem))
                        {
                            accessToken = dataElem.GetProperty("access_token").GetString() ?? "";
                            if (dataElem.TryGetProperty("refresh_token", out var rtElem))
                            {
                                refreshToken = rtElem.GetString() ?? "";
                            }
                            if (dataElem.TryGetProperty("open_id", out var openIdElem))
                            {
                                channelOrPageId = openIdElem.GetString() ?? "";
                            }
                            if (dataElem.TryGetProperty("expires_in", out var expElem))
                            {
                                tokenExpiresAt = DateTime.UtcNow.AddSeconds(expElem.GetInt32());
                            }
                        }

                        accountName = !string.IsNullOrEmpty(channelOrPageId) ? $"TikTok User ({channelOrPageId.Substring(0, Math.Min(6, channelOrPageId.Length))})" : "TikTok Channel";
                        break;
                    }

                case PlatformType.FacebookReels:
                    {
                        // 1. Lấy User Access Token
                        string tokenUrl = $"https://graph.facebook.com/v19.0/oauth/access_token?client_id={_settings.FacebookReels.AppId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&client_secret={_settings.FacebookReels.AppSecret}&code={code}";
                        var response = await client.GetAsync(tokenUrl);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("Lỗi trao đổi token Meta Facebook: {Resp}", responseBody);
                            throw new BadRequestException($"Không thể xác thực với Facebook: {responseBody}");
                        }

                        using var doc = JsonDocument.Parse(responseBody);
                        var userAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";

                        // 2. Lấy danh sách Facebook Pages của người dùng để lấy Page Access Token
                        var pagesResp = await client.GetAsync($"https://graph.facebook.com/v19.0/me/accounts?access_token={userAccessToken}");
                        if (pagesResp.IsSuccessStatusCode)
                        {
                            var pagesBody = await pagesResp.Content.ReadAsStringAsync();
                            using var pagesDoc = JsonDocument.Parse(pagesBody);
                            var data = pagesDoc.RootElement.GetProperty("data");
                            if (data.GetArrayLength() > 0)
                            {
                                var firstPage = data[0];
                                channelOrPageId = firstPage.GetProperty("id").GetString() ?? "";
                                accountName = firstPage.GetProperty("name").GetString() ?? "Facebook Fanpage";
                                accessToken = firstPage.GetProperty("access_token").GetString() ?? userAccessToken;
                            }
                        }

                        if (string.IsNullOrEmpty(accessToken))
                        {
                            accessToken = userAccessToken;
                            accountName = "Facebook Account";
                            channelOrPageId = "fb_me";
                        }
                        break;
                    }
            }

            // Lưu hoặc cập nhật SocialAccount trong cơ sở dữ liệu
            var existing = await _context.SocialAccounts
                .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.Platform == platform && sa.ChannelIdOrPageId == channelOrPageId);

            if (existing != null)
            {
                existing.AccessToken = accessToken;
                if (!string.IsNullOrEmpty(refreshToken)) existing.RefreshToken = refreshToken;
                existing.TokenExpiresAt = tokenExpiresAt;
                existing.AccountName = accountName;
                existing.IsActive = true;
                await _context.SaveChangesAsync();
                return existing.ToDto();
            }

            var newAccount = new SocialAccount
            {
                UserId = userId,
                Platform = platform,
                AccountName = accountName,
                ChannelIdOrPageId = channelOrPageId,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.SocialAccounts.Add(newAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã liên kết kênh {Platform} thành công cho User: {UserId}, Tên: {Name}", platform, userId, accountName);

            return newAccount.ToDto();
        }
    }
}
