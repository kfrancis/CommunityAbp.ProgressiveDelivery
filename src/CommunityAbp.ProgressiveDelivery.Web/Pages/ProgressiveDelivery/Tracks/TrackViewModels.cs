using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Form;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class CreateTrackViewModel
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxTrackNameLength)]
    [DisplayName("Name")]
    public string Name { get; set; } = default!;

    [StringLength(ProgressiveDeliveryConsts.MaxDisplayNameLength)]
    [DisplayName("DisplayName")]
    public string? DisplayName { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    [TextArea(Rows = 3)]
    [DisplayName("Description")]
    public string? Description { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    [DisplayName("LevelZeroDescription")]
    public string? LevelZeroDescription { get; set; } = "Original implementation";

    [DisplayName("IsEnabled")]
    public bool IsEnabled { get; set; } = true;
}

public class EditTrackViewModel
{
    [HiddenInput]
    public Guid Id { get; set; }

    [HiddenInput]
    public string? ConcurrencyStamp { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDisplayNameLength)]
    [DisplayName("DisplayName")]
    public string? DisplayName { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    [TextArea(Rows = 3)]
    [DisplayName("Description")]
    public string? Description { get; set; }

    [DisplayName("IsEnabled")]
    public bool IsEnabled { get; set; }
}

public class AddLevelViewModel
{
    [HiddenInput]
    public Guid TrackId { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    [DisplayName("Description")]
    public string? Description { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSupportDescriptionLength)]
    [TextArea(Rows = 3)]
    [DisplayName("SupportDescription")]
    public string? SupportDescription { get; set; }

    [DisplayName("FallbackPolicy")]
    public FallbackPolicy FallbackPolicy { get; set; } = FallbackPolicy.None;

    [DisplayName("IsPerformanceSensitive")]
    public bool IsPerformanceSensitive { get; set; }
}

public class EditLevelViewModel
{
    [HiddenInput]
    public Guid TrackId { get; set; }

    [HiddenInput]
    public int Level { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    [DisplayName("Description")]
    public string? Description { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSupportDescriptionLength)]
    [TextArea(Rows = 3)]
    [DisplayName("SupportDescription")]
    public string? SupportDescription { get; set; }

    [DisplayName("FallbackPolicy")]
    public FallbackPolicy FallbackPolicy { get; set; }

    [DisplayName("IsPerformanceSensitive")]
    public bool IsPerformanceSensitive { get; set; }
}

public class SetOfficialLevelViewModel
{
    [HiddenInput]
    public Guid TrackId { get; set; }

    [Range(0, int.MaxValue)]
    [DisplayName("OfficialLevel")]
    public int OfficialLevel { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxReasonLength)]
    [TextArea(Rows = 2)]
    [DisplayName("Reason")]
    public string? Reason { get; set; }
}
