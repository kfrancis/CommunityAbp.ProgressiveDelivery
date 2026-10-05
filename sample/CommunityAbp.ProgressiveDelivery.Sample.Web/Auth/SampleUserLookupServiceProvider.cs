using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;

/// <summary>
/// Stands in for the Identity module so the subject pickers can find the sample users by user name, name or email.
/// A real host gets this from <c>Volo.Abp.Identity.Domain</c> (or the Identity HTTP API client) for free.
/// </summary>
[Dependency(TryRegister = true)]
[ExposeServices(typeof(IExternalUserLookupServiceProvider))]
public class SampleUserLookupServiceProvider : IExternalUserLookupServiceProvider, ITransientDependency
{
    private static readonly UserData[] Users =
    [
        new(SamplePersonas.OpsPersona.UserId, SamplePersonas.OpsPersona.UserName, "kfrancis@example.com", "K.", "Francis"),
        new(SamplePersonas.SupportPersona.UserId, SamplePersonas.SupportPersona.UserName, "dana.whitfield@example.com", "Dana", "Whitfield"),
        new(new Guid("7c2a1f48-9b30-4d21-8e4b-2f0c9a771d55"), "pilot.clinic", "pilot@example.com", "Priya", "Lal"),
        new(new Guid("5d8e0b9a-3c71-4f02-9a6e-1b4c7d2e8f30"), "mreyes", "maria.reyes@example.com", "Maria", "Reyes"),
        new(new Guid("9f1c2d3e-4b5a-4c6d-8e7f-0a1b2c3d4e5f"), "jchen", "james.chen@example.com", "James", "Chen")
    ];

    public Task<IUserData?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<IUserData?>(Users.FirstOrDefault(u => u.Id == id));

    public Task<IUserData?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => Task.FromResult<IUserData?>(Users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));

    public Task<List<IUserData>> SearchAsync(string? sorting = null, string? filter = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
        => Task.FromResult(Filter(filter).OrderBy(u => u.UserName).Skip(skipCount).Take(maxResultCount).Cast<IUserData>().ToList());

    public Task<long> GetCountAsync(string? filter = null, CancellationToken cancellationToken = default)
        => Task.FromResult((long)Filter(filter).Count());

    private static IEnumerable<UserData> Filter(string? filter)
        => string.IsNullOrWhiteSpace(filter)
            ? Users
            : Users.Where(u => new[] { u.UserName, u.Email, u.Name, u.Surname }.Any(v => v?.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase) == true));
}
