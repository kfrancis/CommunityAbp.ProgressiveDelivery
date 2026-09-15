using CommunityAbp.ProgressiveDelivery.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Caps the effective level to what the calling client declares it supports, so a mobile app on an older
/// build never receives a level it cannot handle. A client can only lower its level, never raise it.
/// </summary>
[ExposeServices(typeof(IFeatureLevelConstraintProvider))]
public class ClientCapabilityHeaderConstraintProvider : IFeatureLevelConstraintProvider, ITransientDependency
{
    public const string SourceName = "client-capability";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProgressiveDeliveryAspNetCoreOptions _options;

    public ClientCapabilityHeaderConstraintProvider(IHttpContextAccessor httpContextAccessor, IOptions<ProgressiveDeliveryAspNetCoreOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public ValueTask<FeatureLevelConstraint?> GetConstraintAsync(FeatureLevelConstraintContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.HonourCapabilityHeader)
        {
            return ValueTask.FromResult<FeatureLevelConstraint?>(null);
        }

        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null || !headers.TryGetValue(_options.CapabilityHeaderName, out var values))
        {
            return ValueTask.FromResult<FeatureLevelConstraint?>(null);
        }

        var max = ParseCapabilities(values.ToString(), context.TrackName);
        return ValueTask.FromResult(max is { } level
            ? new FeatureLevelConstraint(level, SourceName, $"Client declared maximum level {level} via {_options.CapabilityHeaderName}")
            : null);
    }

    /// <summary>Parses <c>*=3;Track.A=2,Track.B=1</c>; returns the lowest matching level or <c>null</c>.</summary>
    public static int? ParseCapabilities(string header, string trackName)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        int? result = null;
        foreach (var entry in header.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = entry[..separator].Trim();
            if (!int.TryParse(entry[(separator + 1)..].Trim(), out var level) || level < 0)
            {
                continue;
            }

            if (key == "*" || string.Equals(key, trackName, StringComparison.OrdinalIgnoreCase))
            {
                result = result is null ? level : Math.Min(result.Value, level);
            }
        }

        return result;
    }
}
