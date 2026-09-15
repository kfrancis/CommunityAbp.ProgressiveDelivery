using CommunityAbp.ProgressiveDelivery.Subjects;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

/// <summary>
/// Sticky <c>subject + track -> level</c>. Never reset when the official level advances: an assignment
/// below the official level simply resolves to the official level.
/// </summary>
public class FeatureAssignment : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid FeatureTrackId { get; private set; }

    public string SubjectType { get; private set; } = default!;

    public string SubjectId { get; private set; } = default!;

    public int AssignedLevel { get; private set; }

    public string? AssignmentReason { get; private set; }

    protected FeatureAssignment()
    {
    }

    public FeatureAssignment(Guid id, Guid featureTrackId, FeatureSubject subject, int assignedLevel, string? assignmentReason = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(subject);

        FeatureTrackId = featureTrackId;
        TenantId = subject.TenantId;
        SubjectType = Check.NotNullOrWhiteSpace(subject.Type, nameof(subject.Type), ProgressiveDeliveryConsts.MaxSubjectTypeLength);
        SubjectId = Check.NotNullOrWhiteSpace(subject.Id, nameof(subject.Id), ProgressiveDeliveryConsts.MaxSubjectIdLength);
        ChangeLevel(assignedLevel, assignmentReason);
    }

    public FeatureSubject ToSubject() => new(SubjectType, SubjectId, TenantId);

    public FeatureAssignment ChangeLevel(int assignedLevel, string? reason)
    {
        AssignedLevel = Check.Range(assignedLevel, nameof(assignedLevel), 0);
        AssignmentReason = Check.Length(reason, nameof(reason), ProgressiveDeliveryConsts.MaxReasonLength);
        return this;
    }
}
