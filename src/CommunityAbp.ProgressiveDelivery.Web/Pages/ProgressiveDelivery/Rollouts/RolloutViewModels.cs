using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Rollouts;

public class StartRolloutViewModel
{
    [HiddenInput]
    public Guid TrackId { get; set; }

    [Range(1, int.MaxValue)]
    [DisplayName("TargetLevel")]
    public int TargetLevel { get; set; }

    /// <summary>Entered as a percentage with two decimals; converted to basis points.</summary>
    [Range(0, 100)]
    [DisplayName("Percentage")]
    public decimal Percentage { get; set; } = 1;

    [DisplayName("AllowSkippingIntermediateLevels")]
    public bool AllowSkippingIntermediateLevels { get; set; }
}

public class EditRolloutViewModel
{
    [HiddenInput]
    public Guid TrackId { get; set; }

    [HiddenInput]
    public int TargetLevel { get; set; }

    [Range(0, 100)]
    [DisplayName("Percentage")]
    public decimal Percentage { get; set; }

    [DisplayName("Status")]
    public FeatureRolloutStatus Status { get; set; }

    [DisplayName("AllowSkippingIntermediateLevels")]
    public bool AllowSkippingIntermediateLevels { get; set; }
}
