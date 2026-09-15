using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TUnit.Core;
using Volo.Abp;
using Volo.Abp.Autofac;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Hosts the module in an in-memory TestServer. One host per test instance, matching the other test projects.
/// </summary>
public abstract class ProgressiveDeliveryAspNetCoreTestBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private WebApplication _app = default!;

    protected HttpClient Client { get; private set; } = default!;

    protected IServiceProvider ServiceProvider => _app.Services;

    [Before(HookType.Test)]
    public async Task InitializeHostAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProgressiveDeliveryAspNetCoreTestModule).Assembly.GetName().Name,
            EnvironmentName = Environments.Production
        });

        builder.WebHost.UseTestServer();
        builder.Host.UseAutofac();
        await builder.AddApplicationAsync<ProgressiveDeliveryAspNetCoreTestModule>();

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

    protected async Task<T> GetJsonAsync<T>(string url, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        configure?.Invoke(request);

        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue($"{(int)response.StatusCode} {response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    protected async Task<TResponse> PostJsonAsync<TResponse>(string url, object payload, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        configure?.Invoke(request);

        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue($"{(int)response.StatusCode} {response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<TResponse>(body, JsonOptions)!;
    }
}
