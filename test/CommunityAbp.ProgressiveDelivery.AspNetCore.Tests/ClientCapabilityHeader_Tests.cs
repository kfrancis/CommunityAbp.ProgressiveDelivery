using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Client;
using CommunityAbp.ProgressiveDelivery.Inspection;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using TUnit.Core;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery;

public class ClientCapabilityHeader_Tests : ProgressiveDeliveryAspNetCoreTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;
    private static readonly string UserId = ProgressiveDeliveryTestData.UserId.ToString("D");

    [Test]
    [Arguments("*=3", 3)]
    [Arguments("*=2", 2)]
    [Arguments("Test.Claims.Loading=1;Other.Track=0", 1)]
    [Arguments("other.track=0", null)]
    [Arguments("garbage", null)]
    public void Header_Parsing_Picks_The_Lowest_Matching_Level(string header, int? expected)
    {
        ClientCapabilityHeaderConstraintProvider.ParseCapabilities(header, Track).ShouldBe(expected);
    }

    [Test]
    public async Task Capability_Header_Caps_Effective_Level_Over_Http()
    {
        await PostJsonAsync<FeatureAssignmentDto>($"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/assignments/override", new OverrideFeatureAssignmentDto
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Level = 3
        });

        var unconstrained = await PostJsonAsync<List<ResolvedFeatureLevelDto>>(
            $"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/client/resolve",
            new ResolveFeatureLevelsInput { Tracks = [Track] });
        unconstrained.Single().EffectiveLevel.ShouldBe(3);

        var capped = await PostJsonAsync<List<ResolvedFeatureLevelDto>>(
            $"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/client/resolve",
            new ResolveFeatureLevelsInput { Tracks = [Track] },
            request => request.Headers.Add(ProgressiveDeliveryHttpHeaders.Capabilities, "*=2"));
        capped.Single().EffectiveLevel.ShouldBe(2);

        // Body capabilities and header capabilities combine; the lowest wins.
        var bodyCapped = await PostJsonAsync<List<ResolvedFeatureLevelDto>>(
            $"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/client/resolve",
            new ResolveFeatureLevelsInput { Tracks = [Track], Capabilities = new Dictionary<string, int> { [Track] = 1 } },
            request => request.Headers.Add(ProgressiveDeliveryHttpHeaders.Capabilities, "*=2"));
        bodyCapped.Single().EffectiveLevel.ShouldBe(1);
    }

    [Test]
    public async Task Inspection_Endpoint_Reports_Constraint_Source()
    {
        await PostJsonAsync<FeatureAssignmentDto>($"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/assignments/override", new OverrideFeatureAssignmentDto
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Level = 3
        });

        var inspection = await GetJsonAsync<SubjectFeatureInspectionDto>(
            $"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/inspection/subject?trackName={Track}&subjectType=User&subjectId={UserId}",
            request => request.Headers.Add(ProgressiveDeliveryHttpHeaders.Capabilities, $"{Track}=2"));

        inspection.AssignedLevel.ShouldBe(3);
        inspection.EffectiveLevel.ShouldBe(2);
        inspection.Constraints.ShouldHaveSingleItem().Source.ShouldBe(ClientCapabilityHeaderConstraintProvider.SourceName);
        inspection.Differences.Select(d => d.Level).ShouldBe([2]);
    }

    [Test]
    public async Task Track_Management_Endpoints_Round_Trip()
    {
        var page = await GetJsonAsync<PagedResultDto<FeatureTrackDto>>($"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/tracks?filter=Claims");
        page.TotalCount.ShouldBe(1);
        var track = page.Items.Single();

        var level = await PostJsonAsync<FeatureLevelDto>($"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/tracks/{track.Id}/levels", new AddFeatureLevelDto
        {
            Description = "Level 4",
            FallbackPolicy = FallbackPolicy.Idempotent
        });
        level.Level.ShouldBe(4);

        var byName = await GetJsonAsync<FeatureTrackDto>($"/{ProgressiveDeliveryRemoteServiceConsts.RouteRoot}/tracks/by-name/{Track}");
        byName.HighestAvailableLevel.ShouldBe(4);
        byName.Levels.Count.ShouldBe(5);
    }
}
