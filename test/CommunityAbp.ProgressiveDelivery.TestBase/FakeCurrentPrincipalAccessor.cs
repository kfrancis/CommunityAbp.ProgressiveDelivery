using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>Default principal: the seeded test user in the host. Tests use <c>Change</c> to switch identity.</summary>
public class FakeCurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor
{
    protected override ClaimsPrincipal GetClaimsPrincipal() => GetPrincipal();

    public static ClaimsPrincipal GetPrincipal(Guid? userId = null, Guid? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, (userId ?? ProgressiveDeliveryTestData.UserId).ToString()),
            new(AbpClaimTypes.UserName, "test-user"),
            new(AbpClaimTypes.Email, "test-user@example.invalid")
        };

        if (tenantId is not null)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
