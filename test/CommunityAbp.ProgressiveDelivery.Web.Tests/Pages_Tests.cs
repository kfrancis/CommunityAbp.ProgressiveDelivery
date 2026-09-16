using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using TUnit.Core;

namespace CommunityAbp.ProgressiveDelivery.Web;

public class Pages_Tests : ProgressiveDeliveryWebTestBase
{
    private static readonly string UserId = ProgressiveDeliveryTestData.UserId.ToString("D");

    [Test]
    public async Task Tracks_Index_Renders_Kpis_And_Table()
    {
        var html = await GetPageAsync("/ProgressiveDelivery/Tracks");

        html.ShouldContain("TracksTable");
        html.ShouldContain("Feature tracks");
        html.ShouldContain("pd-kpi-value");
        html.ShouldContain("NewTrackButton");
        html.ShouldContain("/Pages/ProgressiveDelivery/Tracks/index.js");
    }

    [Test]
    public async Task Track_Detail_Renders_Level_Line_Levels_And_Tabs()
    {
        var track = await GetRequiredService<IFeatureTrackAppService>().GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack);

        var html = await GetPageAsync($"/ProgressiveDelivery/Tracks/Detail?id={track.Id}");

        html.ShouldContain(ProgressiveDeliveryTestData.ClaimsTrack);
        html.ShouldContain("pd-level-official");
        html.ShouldContain("pd-level-experimental");
        html.ShouldContain("Parallel validation");
        html.ShouldContain("pd-policy-SafeRead");
        html.ShouldContain("AssignmentsTable");
        html.ShouldContain("HistoryTable");
        html.ShouldContain("DeleteTrackButton");
    }

    [Test]
    public async Task Modals_Render()
    {
        var track = await GetRequiredService<IFeatureTrackAppService>().GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack);

        (await GetPageAsync("/ProgressiveDelivery/Tracks/CreateModal")).ShouldContain("Track_Name");
        (await GetPageAsync($"/ProgressiveDelivery/Tracks/EditModal?id={track.Id}")).ShouldContain("Track_DisplayName");
        (await GetPageAsync($"/ProgressiveDelivery/Tracks/AddLevelModal?trackId={track.Id}")).ShouldContain("Add level 4");
        (await GetPageAsync($"/ProgressiveDelivery/Tracks/EditLevelModal?trackId={track.Id}&level=2")).ShouldContain("Level_FallbackPolicy");
        (await GetPageAsync($"/ProgressiveDelivery/Tracks/SetOfficialLevelModal?trackId={track.Id}&level=2")).ShouldContain("Input_OfficialLevel");
        (await GetPageAsync($"/ProgressiveDelivery/Rollouts/StartModal?trackId={track.Id}")).ShouldContain("Input_Percentage");
        (await GetPageAsync($"/ProgressiveDelivery/Assignments/OverrideModal?trackName={ProgressiveDeliveryTestData.ClaimsTrack}&subjectType=User&subjectId={UserId}")).ShouldContain("Input_IsEmergency");
    }

    [Test]
    public async Task Inspection_Page_Shows_Differences_For_Experimental_Subject()
    {
        await GetRequiredService<IFeatureAssignmentAppService>().OverrideAsync(new OverrideFeatureAssignmentDto
        {
            TrackName = ProgressiveDeliveryTestData.ClaimsTrack,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Level = 3,
            Reason = "test"
        });

        var html = await GetPageAsync($"/ProgressiveDelivery/Inspection?subjectType=User&subjectId={UserId}&trackName={ProgressiveDeliveryTestData.ClaimsTrack}");

        html.ShouldContain("Differences from official");
        html.ShouldContain("Adds a read-through cache");
        html.ShouldContain("Validates claims in parallel");
        html.ShouldContain("pd-level-effective");
        html.ShouldContain("pd-inspect-reset");
    }

    [Test]
    public async Task Inspection_Page_Without_Subject_Shows_Form_Only()
    {
        var html = await GetPageAsync("/ProgressiveDelivery/Inspection");

        html.ShouldContain("InspectForm");
        html.ShouldNotContain("pd-inspect-card");
    }

    [Test]
    public async Task Transitions_Page_Renders_Filters_And_Table()
    {
        var html = await GetPageAsync("/ProgressiveDelivery/Transitions");

        html.ShouldContain("TransitionsTable");
        html.ShouldContain("TrackFilter");
        html.ShouldContain(ProgressiveDeliveryTestData.ClaimsTrack);
        html.ShouldContain("Automatic demotion");
    }

    [Test]
    public async Task Menu_Contributor_Adds_Progressive_Delivery_Group()
    {
        var html = await GetPageAsync("/ProgressiveDelivery/Tracks");

        html.ShouldContain("Progressive Delivery");
        html.ShouldContain("/ProgressiveDelivery/Inspection");
        html.ShouldContain("/ProgressiveDelivery/Transitions");
    }
}
