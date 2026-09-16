using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TUnit.Core;
using Volo.Abp;
using Volo.Abp.Autofac;

namespace CommunityAbp.ProgressiveDelivery.Web;

public abstract class ProgressiveDeliveryWebTestBase
{
    private WebApplication _app = default!;

    protected HttpClient Client { get; private set; } = default!;

    protected IServiceProvider ServiceProvider => _app.Services;

    [Before(HookType.Test)]
    public async Task InitializeHostAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProgressiveDeliveryWebTestModule).Assembly.GetName().Name,
            EnvironmentName = Environments.Production
        });

        builder.WebHost.UseTestServer();
        builder.Host.UseAutofac();
        await builder.AddApplicationAsync<ProgressiveDeliveryWebTestModule>();

        _app = builder.Build();
        await _app.InitializeApplicationAsync();
        await _app.StartAsync();

        Client = _app.GetTestClient();
    }

    [After(HookType.Test)]
    public async Task ShutdownHostAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    protected T GetRequiredService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    protected async Task<string> GetPageAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue($"{(int)response.StatusCode} {response.StatusCode} for {url}: {body[..Math.Min(body.Length, 2000)]}");
        return body;
    }
}
