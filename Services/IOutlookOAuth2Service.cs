using MailArchiver.Models;

namespace MailArchiver.Services
{
    /// <summary>
    /// Service interface for Outlook OAuth2 authentication
    /// </summary>
    public interface IOutlookOAuth2Service
    {
        /// <summary>
        /// Generates the OAuth2 authorization URL for Outlook personal accounts
        /// </summary>
        /// <param name="redirectUri">The redirect URI registered in Azure</param>
        /// <param name="state">State parameter for CSRF protection</param>
        /// <returns>Authorization URL</returns>
        string GetAuthorizationUrl(string redirectUri, string state);

        /// <summary>
        /// Exchanges authorization code for access and refresh tokens
        /// </summary>
        /// <param name="code">Authorization code from OAuth2 callback</param>
        /// <param name="redirectUri">The redirect URI used in authorization request</param>
        /// <returns>OAuth2 token response containing access token, refresh token, and expiry</returns>
        Task<OAuth2TokenResponse> ExchangeCodeForTokensAsync(string code, string redirectUri);

        /// <summary>
        /// Refreshes an access token using a refresh token
        /// </summary>
        /// <param name="refreshToken">The refresh token</param>
        /// <returns>New OAuth2 token response</returns>
        Task<OAuth2TokenResponse> RefreshAccessTokenAsync(string refreshToken);

        /// <summary>
        /// Checks if an access token is expired or about to expire
        /// </summary>
        /// <param name="tokenExpiry">Token expiry date time</param>
        /// <returns>True if token needs refresh</returns>
        bool IsTokenExpired(DateTime? tokenExpiry);

        /// <summary>
        /// Gets a valid access token for a mail account, refreshing if necessary
        /// </summary>
        /// <param name="account">The mail account</param>
        /// <returns>Valid access token</returns>
        Task<string> GetValidAccessTokenAsync(MailAccount account);
    }

    /// <summary>
    /// OAuth2 token response
    /// </summary>
    public class OAuth2TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime TokenExpiry { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }
}
