using System.Diagnostics;
using System.Text.Json;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Telemetry;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Tracing;

namespace CommunityAbp.ProgressiveDelivery.Transitions;

/// <summary>
/// Writes <see cref="FeatureTransition"/> rows enriched with correlation and trace identifiers, then notifies telemetry.
/// </summary>
public interface IFeatureTransitionRecorder
{
    Task<FeatureTransition> RecordAsync(
        FeatureTrack track,
        FeatureTransitionType transitionType,
        int? fromLevel,
        int toLevel,
        FeatureSubject? subject = null,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);
}

public class FeatureTransitionRecorder : IFeatureTransitionRecorder, ITransientDependency
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFeatureTransitionRepository _transitionRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IEnumerable<IProgressiveDeliveryTelemetry> _telemetry;
    private readonly ILogger<FeatureTransitionRecorder> _logger;

    public FeatureTransitionRecorder(
        IFeatureTransitionRepository transitionRepository,
        IGuidGenerator guidGenerator,
        ICorrelationIdProvider correlationIdProvider,
        IEnumerable<IProgressiveDeliveryTelemetry> telemetry,
        ILogger<FeatureTransitionRecorder> logger)
    {
        _transitionRepository = transitionRepository;
        _guidGenerator = guidGenerator;
        _correlationIdProvider = correlationIdProvider;
        _telemetry = telemetry;
        _logger = logger;
    }

    public virtual async Task<FeatureTransition> RecordAsync(
        FeatureTrack track,
        FeatureTransitionType transitionType,
        int? fromLevel,
        int toLevel,
        FeatureSubject? subject = null,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var transition = new FeatureTransition(
            _guidGenerator.Create(),
            track.Id,
            track.Name,
            transitionType,
            fromLevel,
            toLevel,
            subject?.Type,
            subject?.Id,
            subject?.TenantId,
            reason,
            _correlationIdProvider.Get(),
            Activity.Current?.TraceId.ToString(),
            metadata is { Count: > 0 } ? JsonSerializer.Serialize(metadata, MetadataJsonOptions) : null);

        await _transitionRepository.InsertAsync(transition, autoSave: true, cancellationToken);

        _logger.LogInformation(
            "Progressive delivery transition {TransitionType} on track {Track}: {FromLevel} -> {ToLevel} for {SubjectType} ({Reason})",
            transitionType, track.Name, fromLevel, toLevel, subject?.Type ?? "track", reason);

        var telemetry = new FeatureTransitionTelemetry(track.Name, transitionType, fromLevel, toLevel, subject?.Type, reason);
        foreach (var listener in _telemetry)
        {
            try
            {
                listener.RecordTransition(telemetry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telemetry listener {Listener} threw while recording a transition.", listener.GetType().FullName);
            }
        }

        return transition;
    }
}
