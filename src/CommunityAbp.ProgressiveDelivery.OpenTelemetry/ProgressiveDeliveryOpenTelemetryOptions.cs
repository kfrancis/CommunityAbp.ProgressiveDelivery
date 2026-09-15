namespace CommunityAbp.ProgressiveDelivery.OpenTelemetry;

public class ProgressiveDeliveryOpenTelemetryOptions
{
    /// <summary>
    /// Emit <c>progressive_delivery.subject_id</c> on activities. Off by default: subject ids are usually user ids.
    /// </summary>
    public bool IncludeSubjectId { get; set; }

    /// <summary>Record the exception on the activity (<c>Activity.AddException</c>) when a route fails. Default <c>true</c>.</summary>
    public bool RecordExceptions { get; set; } = true;
}
