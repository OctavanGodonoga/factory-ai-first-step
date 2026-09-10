using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TalentMap.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["Authentication:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            Logger.LogError(
                "Authentication:ApiKey is not configured; rejecting all requests to API-key protected endpoints.");
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var providedKeyValues) ||
            string.IsNullOrWhiteSpace(providedKeyValues))
        {
            Logger.LogWarning("Request missing required {HeaderName} header.", ApiKeyAuthenticationOptions.HeaderName);
            return Task.FromResult(AuthenticateResult.Fail($"Missing {ApiKeyAuthenticationOptions.HeaderName} header."));
        }

        var providedKey = providedKeyValues.ToString();
        var keysMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey),
            Encoding.UTF8.GetBytes(configuredKey));

        if (!keysMatch)
        {
            Logger.LogWarning("Request presented an invalid API key.");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "map-points-client") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
