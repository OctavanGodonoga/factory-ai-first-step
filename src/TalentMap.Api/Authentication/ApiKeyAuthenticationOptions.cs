using Microsoft.AspNetCore.Authentication;

namespace TalentMap.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    public const string HeaderName = "X-Api-Key";
}
