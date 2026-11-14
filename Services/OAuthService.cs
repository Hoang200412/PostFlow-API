using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        private readonly IAuthService _authService;

        public OAuthService(
            AppDbContext context,
            IOptions<PlatformSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<OAuthService> logger,
            IAuthService authService)
        {
            _context = context;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _authService = authService;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            // TikTok yêu cầu code_challenge theo định dạng SHA-256 mã hóa HEX viết thường (khác với chuẩn RFC 7636 Base64URL)
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public string GenerateState(PlatformType platform, string? returnUrl = null)
        {
            string? codeVerifier = null;
            string? codeChallenge = null;

            if (platform == PlatformType.TikTok)
            {
                codeVerifier = GenerateCodeVerifier();
                codeChallenge = GenerateCodeChallenge(codeVerifier);
            }

            var payload = new
            {
                platform = (int)platform,
                returnUrl = returnUrl ?? string.Empty,
                codeVerifier,
                codeChallenge
            };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
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
                    var configuredScopes = !string.IsNullOrWhiteSpace(_settings.TikTok.Scopes)
                        ? _settings.TikTok.Scopes
                        : "user.info.basic,video.publish";
                    var ttScopes = Uri.EscapeDataString(configuredScopes);

                    string codeChallenge = "";
                    try
                    {
                        var rawState = state.Replace('-', '+').Replace('_', '/');
                        var pad = rawState.Length % 4;
                        if (pad == 2) rawState += "==";
                        else if (pad == 3) rawState += "=";
                        var stateJson = Encoding.UTF8.GetString(Convert.FromBase64String(rawState));
                        using var stateDoc = JsonDocument.Parse(stateJson);
                        if (stateDoc.RootElement.TryGetProperty("codeChallenge", out var ccElem))
                        {
                            codeChallenge = ccElem.GetString() ?? "";
                        }
                    }
                    catch { }

                    var challengeParam = !string.IsNullOrEmpty(codeChallenge)
                        ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256"
                        : "";

                    return $"https://www.tiktok.com/v2/auth/authorize/?client_key={ttClientKey}&scope={ttScopes}&response_type=code&redirect_uri={encodedRedirect}&state={encodedState}{challengeParam}";

                case PlatformType.FacebookReels:
                    var fbAppId = _settings.FacebookReels.AppId;
                    var fbScopes = Uri.EscapeDataString("pages_show_list,pages_read_engagement,pages_manage_posts,publish_video");
                    return $"https://www.facebook.com/v19.0/dialog/oauth?client_id={fbAppId}&redirect_uri={encodedRedirect}&scope={fbScopes}&state={encodedState}&response_type=code";

                default:
                    throw new BadRequestException($"Nền tảng {platform} không hỗ trợ đăng nhập OAuth.");
            }
        }

        public string GetSocialLoginAuthorizationUrl(string provider, string redirectUri, out string state)
        {
            var normalizedProvider = provider.Trim().ToLowerInvariant();
            if (normalizedProvider is not ("google" or "facebook"))
            {
                throw new BadRequestException("Chỉ hỗ trợ đăng nhập bằng Google hoặc Facebook.");
            }

            state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var encodedRedirect = Uri.EscapeDataString(redirectUri);
            var encodedState = Uri.EscapeDataString(state);

            var fbLoginAppId = !string.IsNullOrWhiteSpace(_settings.FacebookReels.LoginAppId)
                ? _settings.FacebookReels.LoginAppId
                : _settings.FacebookReels.AppId;

            return normalizedProvider == "google"
                ? $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(_settings.YouTube.ClientId)}&redirect_uri={encodedRedirect}&response_type=code&scope={Uri.EscapeDataString("openid email profile")}&state={encodedState}&access_type=offline&prompt=select_account"
                : $"https://www.facebook.com/v19.0/dialog/oauth?client_id={Uri.EscapeDataString(fbLoginAppId)}&redirect_uri={encodedRedirect}&response_type=code&scope={Uri.EscapeDataString("public_profile,email")}&state={encodedState}";
        }

        public async Task<AuthResponseDto> HandleSocialLoginCallbackAsync(string provider, string code, string redirectUri, string? state)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BadRequestException("Authorization code không hợp lệ.");
            }

            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var client = _httpClientFactory.CreateClient();
            string accessToken;
            string refreshToken = string.Empty;
            DateTime? tokenExpiresAt = null;
            string providerUserId;
            string email = string.Empty;
            string displayName = normalizedProvider;
            string avatarUrl = string.Empty;

            if (normalizedProvider == "google")
            {
                var tokenParams = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _settings.YouTube.ClientId,
                    ["client_secret"] = _settings.YouTube.ClientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                };
                using var tokenResponse = await client.PostAsync(
                    "https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(tokenParams));
                var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google social login token exchange failed: {Response}", tokenBody);
                    throw new BadRequestException("Không thể xác thực với Google.");
                }

                using var tokenDoc = JsonDocument.Parse(tokenBody);
                accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
                refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var googleRefresh)
                    ? googleRefresh.GetString() ?? string.Empty
                    : string.Empty;
                if (tokenDoc.RootElement.TryGetProperty("expires_in", out var googleExpiry))
                {
                    tokenExpiresAt = DateTime.UtcNow.AddSeconds(googleExpiry.GetInt32());
                }

                using var profileRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://openidconnect.googleapis.com/v1/userinfo");
                profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var profileResponse = await client.SendAsync(profileRequest);
                var profileBody = await profileResponse.Content.ReadAsStringAsync();
                if (!profileResponse.IsSuccessStatusCode)
                {
                    throw new BadRequestException("Không thể lấy thông tin tài khoản Google.");
                }

                using var profileDoc = JsonDocument.Parse(profileBody);
                providerUserId = profileDoc.RootElement.GetProperty("sub").GetString() ?? string.Empty;
                email = profileDoc.RootElement.TryGetProperty("email", out var googleEmail)
                    ? googleEmail.GetString() ?? string.Empty
                    : string.Empty;
                displayName = profileDoc.RootElement.TryGetProperty("name", out var googleName)
                    ? googleName.GetString() ?? "Google User"
                    : "Google User";
                avatarUrl = profileDoc.RootElement.TryGetProperty("picture", out var googlePicture)
                    ? googlePicture.GetString() ?? string.Empty
                    : string.Empty;
            }
            else if (normalizedProvider == "facebook")
            {
                var fbLoginAppId = !string.IsNullOrWhiteSpace(_settings.FacebookReels.LoginAppId)
                    ? _settings.FacebookReels.LoginAppId
                    : _settings.FacebookReels.AppId;
                var fbLoginAppSecret = !string.IsNullOrWhiteSpace(_settings.FacebookReels.LoginAppSecret)
                    ? _settings.FacebookReels.LoginAppSecret
                    : _settings.FacebookReels.AppSecret;

                var tokenUrl = $"https://graph.facebook.com/v19.0/oauth/access_token?client_id={Uri.EscapeDataString(fbLoginAppId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&client_secret={Uri.EscapeDataString(fbLoginAppSecret)}&code={Uri.EscapeDataString(code)}";
                using var tokenResponse = await client.GetAsync(tokenUrl);
                var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Facebook social login token exchange failed: {Response}", tokenBody);
                    throw new BadRequestException("Không thể xác thực với Facebook.");
                }

                using var tokenDoc = JsonDocument.Parse(tokenBody);
                accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
                if (tokenDoc.RootElement.TryGetProperty("expires_in", out var facebookExpiry))
                {
                    tokenExpiresAt = DateTime.UtcNow.AddSeconds(facebookExpiry.GetInt32());
                }

                using var profileResponse = await client.GetAsync(
                    $"https://graph.facebook.com/me?fields=id,name,email,picture.width(500).height(500)&access_token={Uri.EscapeDataString(accessToken)}");
                var profileBody = await profileResponse.Content.ReadAsStringAsync();
                if (!profileResponse.IsSuccessStatusCode)
                {
                    throw new BadRequestException("Không thể lấy thông tin tài khoản Facebook.");
                }

                using var profileDoc = JsonDocument.Parse(profileBody);
                providerUserId = profileDoc.RootElement.GetProperty("id").GetString() ?? string.Empty;
                email = profileDoc.RootElement.TryGetProperty("email", out var facebookEmail)
                    ? facebookEmail.GetString() ?? string.Empty
                    : string.Empty;
                displayName = profileDoc.RootElement.TryGetProperty("name", out var facebookName)
                    ? facebookName.GetString() ?? "Facebook User"
                    : "Facebook User";
                if (profileDoc.RootElement.TryGetProperty("picture", out var fbPic) &&
                    fbPic.TryGetProperty("data", out var picData) &&
                    picData.TryGetProperty("url", out var picUrl))
                {
                    avatarUrl = picUrl.GetString() ?? string.Empty;
                }
            }
            else
            {
                throw new BadRequestException("Chỉ hỗ trợ đăng nhập bằng Google hoặc Facebook.");
            }

            return await _authService.LoginExternalAsync(new ExternalIdentity
            {
                Provider = normalizedProvider,
                Platform = normalizedProvider == "google" ? PlatformType.YouTube : PlatformType.FacebookReels,
                ProviderUserId = providerUserId,
                Email = email,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt
            });
        }

        public async Task<SocialAccountDto> HandleCallbackAsync(string userId, PlatformType platform, string code, string redirectUri, string? codeVerifier = null)
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

                        if (!profileResp.IsSuccessStatusCode)
                        {
                            throw new BadRequestException("Không thể lấy thông tin kênh YouTube.");
                        }

                        var profileBody = await profileResp.Content.ReadAsStringAsync();
                        using var profileDoc = JsonDocument.Parse(profileBody);
                        var items = profileDoc.RootElement.GetProperty("items");
                        if (items.GetArrayLength() == 0)
                        {
                            throw new BadRequestException("Tài khoản YouTube không có kênh để kết nối.");
                        }

                        var firstItem = items[0];
                        channelOrPageId = firstItem.GetProperty("id").GetString() ?? string.Empty;
                        accountName = firstItem.GetProperty("snippet").GetProperty("title").GetString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(channelOrPageId) || string.IsNullOrWhiteSpace(accountName))
                        {
                            throw new BadRequestException("Thông tin kênh YouTube không hợp lệ.");
                        }
                        break;
                    }

                case PlatformType.TikTok:
                    {
                        var rawCode = Uri.UnescapeDataString(code ?? string.Empty);
                        var tokenRequestParams = new Dictionary<string, string>
                        {
                            { "client_key", _settings.TikTok.ClientKey },
                            { "client_secret", _settings.TikTok.ClientSecret },
                            { "code", rawCode },
                            { "grant_type", "authorization_code" },
                            { "redirect_uri", redirectUri }
                        };

                        if (!string.IsNullOrEmpty(codeVerifier))
                        {
                            tokenRequestParams.Add("code_verifier", codeVerifier);
                        }

                        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/token/", new FormUrlEncodedContent(tokenRequestParams));
                        var responseBody = await response.Content.ReadAsStringAsync();

                        _logger.LogInformation("Phản hồi trao đổi token TikTok (Status {Status}): {Resp}", (int)response.StatusCode, responseBody);

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("Lỗi HTTP trao đổi token TikTok: {Resp}", responseBody);
                            throw new BadRequestException($"Không thể xác thực với TikTok (HTTP {(int)response.StatusCode}): {responseBody}");
                        }

                        using var doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;

                        // Kiểm tra nếu TikTok trả về object error (TikTok v2 có thể trả về HTTP 200 kèm payload error)
                        if (root.TryGetProperty("error", out var errorElem))
                        {
                            string errorMsg = "";
                            if (errorElem.ValueKind == JsonValueKind.Object)
                            {
                                var errCode = errorElem.TryGetProperty("code", out var c) ? c.GetString() : "";
                                var message = errorElem.TryGetProperty("message", out var m) ? m.GetString() : "";
                                errorMsg = $"{errCode}: {message}".Trim();
                            }
                            else if (errorElem.ValueKind == JsonValueKind.String)
                            {
                                var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : "";
                                errorMsg = $"{errorElem.GetString()} - {desc}".Trim();
                            }

                            if (!string.IsNullOrWhiteSpace(errorMsg) && !errorMsg.Equals("ok", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogError("TikTok từ chối cấp token: {Error} | Body: {Body}", errorMsg, responseBody);
                                throw new BadRequestException($"TikTok từ chối cấp token: {errorMsg}");
                            }
                        }

                        // Lấy data từ object "data" hoặc trực tiếp tại root
                        var dataElem = root.TryGetProperty("data", out var dElem) ? dElem : root;

                        if (dataElem.TryGetProperty("access_token", out var atElem))
                        {
                            accessToken = atElem.GetString() ?? "";
                        }
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

                        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(channelOrPageId))
                        {
                            _logger.LogError("Không tìm thấy access_token hoặc open_id hợp lệ từ TikTok. Body: {Body}", responseBody);
                            throw new BadRequestException($"TikTok không trả về token hoặc định danh tài khoản hợp lệ. Phản hồi TikTok: {responseBody}");
                        }

                        accountName = !string.IsNullOrEmpty(channelOrPageId) ? $"TikTok User ({channelOrPageId.Substring(0, Math.Min(6, channelOrPageId.Length))})" : "TikTok Channel";

                        // Tự động gọi TikTok User Info API để lấy Tên hiển thị (display_name), Avatar và Username thực tế
                        try
                        {
                            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name,avatar_url,username");
                            userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                            var userResp = await client.SendAsync(userRequest);
                            if (userResp.IsSuccessStatusCode)
                            {
                                var userBody = await userResp.Content.ReadAsStringAsync();
                                using var userDoc = JsonDocument.Parse(userBody);
                                if (userDoc.RootElement.TryGetProperty("data", out var uData) &&
                                    uData.TryGetProperty("user", out var uObj))
                                {
                                    var displayName = uObj.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                                    var username = uObj.TryGetProperty("username", out var un) ? un.GetString() : null;

                                    if (!string.IsNullOrWhiteSpace(displayName))
                                    {
                                        accountName = displayName;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(username))
                                    {
                                        accountName = username;
                                    }

                                    _logger.LogInformation("Đã lấy thông tin profile TikTok thành công: {Name} (@{Username})", accountName, username);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Không thể lấy thông tin profile chi tiết từ TikTok user/info, sử dụng tên mặc định.");
                        }

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

                        // 2. Lấy danh sách TẤT CẢ Facebook Pages của người dùng để lấy Page Access Token
                        var pagesResp = await client.GetAsync($"https://graph.facebook.com/v19.0/me/accounts?fields=id,name,category,access_token&access_token={userAccessToken}");
                        if (!pagesResp.IsSuccessStatusCode)
                        {
                            throw new BadRequestException("Không thể lấy danh sách Facebook Page từ tài khoản của bạn.");
                        }

                        var pagesBody = await pagesResp.Content.ReadAsStringAsync();
                        using var pagesDoc = JsonDocument.Parse(pagesBody);
                        var data = pagesDoc.RootElement.GetProperty("data");
                        if (data.GetArrayLength() == 0)
                        {
                            throw new BadRequestException("Tài khoản Facebook của bạn hiện không có quyền quản lý bất kỳ Fanpage nào.");
                        }

                        // 3. Tự động liên kết TẤT CẢ các Fanpage mà tài khoản quản lý
                        SocialAccountDto? primaryAccountDto = null;
                        int savedCount = 0;

                        foreach (var pageElem in data.EnumerateArray())
                        {
                            var pageId = pageElem.GetProperty("id").GetString() ?? string.Empty;
                            var pageName = pageElem.GetProperty("name").GetString() ?? string.Empty;
                            var pageToken = pageElem.GetProperty("access_token").GetString() ?? string.Empty;

                            if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageToken))
                            {
                                continue;
                            }

                            var existingPage = await _context.SocialAccounts
                                .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.Platform == PlatformType.FacebookReels && sa.ChannelIdOrPageId == pageId);

                            if (existingPage != null)
                            {
                                existingPage.AccessToken = pageToken;
                                existingPage.AccountName = pageName;
                                existingPage.IsActive = true;
                                if (primaryAccountDto == null) primaryAccountDto = existingPage.ToDto();
                            }
                            else
                            {
                                var newPageAccount = new SocialAccount
                                {
                                    UserId = userId,
                                    Platform = PlatformType.FacebookReels,
                                    AccountName = pageName,
                                    ChannelIdOrPageId = pageId,
                                    AccessToken = pageToken,
                                    IsActive = true,
                                    CreatedAt = DateTime.UtcNow
                                };
                                _context.SocialAccounts.Add(newPageAccount);
                                if (primaryAccountDto == null) primaryAccountDto = newPageAccount.ToDto();
                            }

                            savedCount++;
                        }

                        if (savedCount == 0)
                        {
                            throw new BadRequestException("Không tìm thấy thông tin xác thực hợp lệ của Fanpage nào.");
                        }

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Đã tự động liên kết thành công {Count} Facebook Pages cho User: {UserId}", savedCount, userId);

                        return primaryAccountDto!;
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Phát hiện race condition khi lưu SocialAccount, tiến hành nạp lại bản ghi hiện tại.");
                var fresh = await _context.SocialAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.Platform == platform && sa.ChannelIdOrPageId == channelOrPageId);
                if (fresh != null) return fresh.ToDto();
                throw;
            }

            _logger.LogInformation("Đã liên kết kênh {Platform} thành công cho User: {UserId}, Tên: {Name}", platform, userId, accountName);

            return newAccount.ToDto();
        }
    }
}
