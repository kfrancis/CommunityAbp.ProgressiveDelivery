using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web;

[Dependency(ReplaceServices = true)]
public class SampleBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "Progressive Delivery sample";
}
