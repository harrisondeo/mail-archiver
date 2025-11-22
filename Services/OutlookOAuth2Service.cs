using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace MailArchiver.Services
{
    /// <summary>
    /// Service for handling Outlook OAuth2 authentication
    /// </summary>
    public class OutlookOAuth2Service : IOutlookOAuth2Service
    {
        private readonly ILogger<OutlookOAuth2Service> _logger;
        private readonly IConfiguration _configuration;
        private readonly MailArchiverDbContext _context;
        private readonly HttpClient _httpClient;

        // OAuth2 endpoints for Microsoft personal accounts
        private const string AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        private const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        
        // Required scopes for IMAP access
        // Note: We use outlook.office.com scope (not graph.microsoft.com) because we're accessing 
        // Outlook's native IMAP service directly, not through Microsoft Graph API
        private const string Scopes = "offline_access https://outlook.office.com/IMAP.AccessAsUser.All";

        public OutlookOAuth2Service(
            ILogger<OutlookOAuth2Service> logger,
            IConfiguration configuration,
            MailArchiverDbContext context,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
        }

        public string GetAuthorizationUrl(string redirectUri, string state)
        {
            var clientId = _configuration["OAuth2:Outlook:ClientId"];
            
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Outlook OAuth2 ClientId is not configured. Please set OAuth2:Outlook:ClientId in configuration.");
            }

            var queryParams = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "response_type", "code" },
                { "redirect_uri", redirectUri },
                { "scope", Scopes },
                { "state", state },
                { "response_mode", "query" }
            };

            var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var authUrl = $"{AuthorizationEndpoint}?{queryString}";

            _logger.LogInformation("Generated OAuth2 authorization URL for Outlook");
            return authUrl;
        }

        public async Task<OAuth2TokenResponse> ExchangeCodeForTokensAsync(string code, string redirectUri)
        {
            var clientId = _configuration["OAuth2:Outlook:ClientId"];
            var clientSecret = _configuration["OAuth2:Outlook:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Outlook OAuth2 credentials are not configured. Please set OAuth2:Outlook:ClientId and OAuth2:Outlook:ClientSecret in configuration.");
            }

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            try
            {
                _logger.LogInformation("Exchanging authorization code for tokens");
                var response = await _httpClient.PostAsync(TokenEndpoint, requestBody);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to exchange code for tokens. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    throw new InvalidOperationException($"Failed to exchange authorization code for tokens: {response.StatusCode} - {responseContent}");
                }

                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                var accessToken = tokenResponse.GetProperty("access_token").GetString();
                var refreshToken = tokenResponse.GetProperty("refresh_token").GetString();
                var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();
                var tokenType = tokenResponse.TryGetProperty("token_type", out var tokenTypeElement) 
                    ? tokenTypeElement.GetString() 
                    : "Bearer";

                _logger.LogInformation("Successfully exchanged authorization code for tokens. Token expires in {ExpiresIn} seconds", expiresIn);

                return new OAuth2TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn),
                    TokenType = tokenType,
                    ExpiresIn = expiresIn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging authorization code for tokens");
                throw;
            }
        }

        public async Task<OAuth2TokenResponse> RefreshAccessTokenAsync(string refreshToken)
        {
            var clientId = _configuration["OAuth2:Outlook:ClientId"];
            var clientSecret = _configuration["OAuth2:Outlook:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Outlook OAuth2 credentials are not configured.");
            }

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            try
            {
                _logger.LogInformation("Refreshing access token");
                var response = await _httpClient.PostAsync(TokenEndpoint, requestBody);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to refresh access token. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    throw new InvalidOperationException($"Failed to refresh access token: {response.StatusCode} - {responseContent}");
                }

                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                var accessToken = tokenResponse.GetProperty("access_token").GetString();
                var newRefreshToken = tokenResponse.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString()
                    : refreshToken; // Some providers don't return a new refresh token
                var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();
                var tokenType = tokenResponse.TryGetProperty("token_type", out var tokenTypeElement) 
                    ? tokenTypeElement.GetString() 
                    : "Bearer";

                _logger.LogInformation("Successfully refreshed access token. Token expires in {ExpiresIn} seconds", expiresIn);

                return new OAuth2TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn),
                    TokenType = tokenType,
                    ExpiresIn = expiresIn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing access token");
                throw;
            }
        }

        public bool IsTokenExpired(DateTime? tokenExpiry)
        {
            if (!tokenExpiry.HasValue)
            {
                return true;
            }

            // Consider token expired if it expires within the next 5 minutes
            return tokenExpiry.Value.AddMinutes(-5) <= DateTime.UtcNow;
        }

        public async Task<string> GetValidAccessTokenAsync(MailAccount account)
        {
            if (account.Provider != ProviderType.OUTLOOK)
            {
                throw new InvalidOperationException($"Account {account.Name} is not an Outlook OAuth2 account");
            }

            // Check if we need to refresh the token
            if (IsTokenExpired(account.TokenExpiry))
            {
                _logger.LogInformation("Access token expired for account {AccountName}, refreshing", account.Name);

                if (string.IsNullOrEmpty(account.RefreshToken))
                {
                    throw new InvalidOperationException($"No refresh token available for account {account.Name}. Please re-authorize the account.");
                }

                var tokenResponse = await RefreshAccessTokenAsync(account.RefreshToken);

                // Update account with new tokens
                account.AccessToken = tokenResponse.AccessToken;
                account.RefreshToken = tokenResponse.RefreshToken;
                account.TokenExpiry = tokenResponse.TokenExpiry;

                // Save to database
                _context.Entry(account).Property(a => a.AccessToken).IsModified = true;
                _context.Entry(account).Property(a => a.RefreshToken).IsModified = true;
                _context.Entry(account).Property(a => a.TokenExpiry).IsModified = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully refreshed and saved access token for account {AccountName}", account.Name);
            }

            if (string.IsNullOrEmpty(account.AccessToken))
            {
                throw new InvalidOperationException($"No access token available for account {account.Name}");
            }

            return account.AccessToken;
        }
    }
}
