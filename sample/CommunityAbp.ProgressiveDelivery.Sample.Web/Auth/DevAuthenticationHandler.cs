using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Volo.Abp.Security.Claims;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;

/// <summary>
/// Signs every request in as a sample user. The <c>pd-role</c> cookie selects the persona:
/// <c>ops</c> (default, all permissions) or <c>support</c> (inspection only). Never use outside the sample.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SampleDev";
    public const string RoleCookie = "pd-role";
    public const string RoleClaim = "pd-role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var persona = SamplePersonas.Resolve(Request.Cookies[RoleCookie]);

        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, persona.UserId.ToString("D")),
            new(AbpClaimTypes.UserName, persona.UserName),
            new(AbpClaimTypes.Name, persona.DisplayName),
            new(AbpClaimTypes.Email, persona.UserName + "@example.invalid"),
            new(RoleClaim, persona.Role)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

public sealed record SamplePersona(string Role, Guid UserId, string UserName, string DisplayName);

public static class SamplePersonas
{
    public const string Ops = "ops";
    public const string Support = "support";

    public static readonly SamplePersona OpsPersona = new(Ops, new Guid("11111111-1111-1111-1111-111111111111"), "kfrancis", "K. Francis (operations)");
    public static readonly SamplePersona SupportPersona = new(Support, new Guid("22222222-2222-2222-2222-222222222222"), "dwhitfield", "Dana Whitfield (support)");

    public static SamplePersona Resolve(string? role) => string.Equals(role, Support, StringComparison.OrdinalIgnoreCase) ? SupportPersona : OpsPersona;
}
