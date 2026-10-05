using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

/// <summary>Tenant-aware in-memory user directory, like Identity's repository-backed provider.</summary>
public class FakeExternalUserLookupServiceProvider : IExternalUserLookupServiceProvider
{
    public static readonly Guid TenantUserId = new("33333333-3333-3333-3333-333333333333");

    private static readonly UserData[] Users =
    [
        new(ProgressiveDeliveryTestData.UserId, "alice", "alice@example.com", "Alice", "Anders"),
        new(ProgressiveDeliveryTestData.OtherUserId, "bob", "bob@contoso.test", "Bob", "Baker"),
        new(TenantUserId, "carol", "carol@acme.test", "Carol", "Cho", tenantId: ProgressiveDeliveryTestData.TenantA)
    ];

    private readonly ICurrentTenant _currentTenant;

    public FakeExternalUserLookupServiceProvider(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public Task<IUserData?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<IUserData?>(InTenant().FirstOrDefault(u => u.Id == id));

    public Task<IUserData?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => Task.FromResult<IUserData?>(InTenant().FirstOrDefault(u => u.UserName == userName));

    public Task<List<IUserData>> SearchAsync(string? sorting = null, string? filter = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
        => Task.FromResult(Filter(filter).Skip(skipCount).Take(maxResultCount).Cast<IUserData>().ToList());

    public Task<long> GetCountAsync(string? filter = null, CancellationToken cancellationToken = default)
        => Task.FromResult((long)Filter(filter).Count());

    private IEnumerable<UserData> InTenant() => Users.Where(u => u.TenantId == _currentTenant.Id);

    private IEnumerable<UserData> Filter(string? filter)
        => string.IsNullOrWhiteSpace(filter)
            ? InTenant()
            : InTenant().Where(u => new[] { u.UserName, u.Email, u.Name, u.Surname }.Any(v => v?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true));
}
