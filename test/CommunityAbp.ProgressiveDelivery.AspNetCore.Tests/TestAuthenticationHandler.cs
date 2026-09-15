using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Security.Claims;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Authenticates every request as a user chosen via the <c>X-Test-User</c> header (defaults to the seeded test user).
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string TenantHeader = "X-Test-Tenant";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue(UserHeader, out var user) && Guid.TryParse(user, out var parsedUser)
            ? parsedUser
            : ProgressiveDeliveryTestData.UserId;

        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString("D")),
            new(AbpClaimTypes.UserName, "test-user")
        };

        if (Request.Headers.TryGetValue(TenantHeader, out var tenant) && Guid.TryParse(tenant, out var tenantId))
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId.ToString("D")));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
