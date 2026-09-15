using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Transitions;

/// <summary>
/// Durable record of a state change (promotion, demotion, override, official change, ...).
/// This is history, not request telemetry: one row per change, never one row per execution.
/// The acting user is captured by <see cref="CreationAuditedAggregateRoot{TKey}.CreatorId"/>.
/// </summary>
public class FeatureTransition : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid FeatureTrackId { get; private set; }

    /// <summary>Denormalised so history survives track deletion and is queryable without a join.</summary>
    public string TrackName { get; private set; } = default!;

    /// <summary><c>null</c> for track-level transitions such as an official level change.</summary>
    public string? SubjectType { get; private set; }

    public string? SubjectId { get; private set; }

    public int? FromLevel { get; private set; }

    public int ToLevel { get; private set; }

    public FeatureTransitionType TransitionType { get; private set; }

    public string? Reason { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? TraceId { get; private set; }

    /// <summary>Optional JSON blob with diagnostic context (exception type, application name, ...).</summary>
    public string? Metadata { get; private set; }

    protected FeatureTransition()
    {
    }

    public FeatureTransition(
        Guid id,
        Guid featureTrackId,
        string trackName,
        FeatureTransitionType transitionType,
        int? fromLevel,
        int toLevel,
        string? subjectType = null,
        string? subjectId = null,
        Guid? tenantId = null,
        string? reason = null,
        string? correlationId = null,
        string? traceId = null,
        string? metadata = null)
        : base(id)
    {
        FeatureTrackId = featureTrackId;
        TrackName = Check.NotNullOrWhiteSpace(trackName, nameof(trackName), ProgressiveDeliveryConsts.MaxTrackNameLength);
        TransitionType = transitionType;
        FromLevel = fromLevel;
        ToLevel = toLevel;
        SubjectType = Check.Length(subjectType, nameof(subjectType), ProgressiveDeliveryConsts.MaxSubjectTypeLength);
        SubjectId = Check.Length(subjectId, nameof(subjectId), ProgressiveDeliveryConsts.MaxSubjectIdLength);
        TenantId = tenantId;
        Reason = Truncate(reason, ProgressiveDeliveryConsts.MaxReasonLength);
        CorrelationId = Truncate(correlationId, ProgressiveDeliveryConsts.MaxCorrelationIdLength);
        TraceId = Truncate(traceId, ProgressiveDeliveryConsts.MaxTraceIdLength);
        Metadata = Truncate(metadata, ProgressiveDeliveryConsts.MaxMetadataLength);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
