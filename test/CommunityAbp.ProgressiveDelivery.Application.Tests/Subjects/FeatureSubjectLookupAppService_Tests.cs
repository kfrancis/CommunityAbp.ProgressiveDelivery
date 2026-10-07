using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Transitions;
using TUnit.Core;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

public class FeatureSubjectLookupAppService_Tests : ProgressiveDeliveryApplicationTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;

    private IFeatureSubjectLookupAppService Lookup => GetRequiredService<IFeatureSubjectLookupAppService>();

    private IFeatureAssignmentAppService Assignments => GetRequiredService<IFeatureAssignmentAppService>();

    [Test]
    public async Task Types_Mark_User_And_Tenant_As_Searchable()
    {
        var types = (await Lookup.GetTypesAsync()).Items;

        types.Single(t => t.Name == FeatureSubjectTypes.User).IsSearchable.ShouldBeTrue();
        types.Single(t => t.Name == FeatureSubjectTypes.Tenant).IsSearchable.ShouldBeTrue();
        types.Single(t => t.Name == FeatureSubjectTypes.Client).IsSearchable.ShouldBeFalse();
    }

    [Test]
    [Arguments("ali")]
    [Arguments("alice@example")]
    [Arguments("ANDERS")]
    public async Task Users_Are_Found_By_User_Name_Email_Or_Name(string filter)
    {
        var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = "user", Filter = filter });

        var hit = result.Items.ShouldHaveSingleItem();
        hit.SubjectType.ShouldBe(FeatureSubjectTypes.User);
        hit.SubjectId.ShouldBe(ProgressiveDeliveryTestData.UserId.ToString("D"));
        hit.DisplayName.ShouldBe("alice");
        hit.Detail.ShouldBe("Alice Anders · alice@example.com");
    }

    [Test]
    public async Task A_Pasted_User_Id_Resolves_To_The_User()
    {
        var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput
        {
            SubjectType = FeatureSubjectTypes.User,
            Filter = ProgressiveDeliveryTestData.OtherUserId.ToString("B").ToUpperInvariant()
        });

        result.Items.ShouldHaveSingleItem().DisplayName.ShouldBe("bob");
    }

    [Test]
    public async Task Host_Searches_Users_Inside_The_Picked_Tenant()
    {
        var hostResult = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.User, Filter = "carol" });
        hostResult.Items.ShouldBeEmpty();

        var tenantResult = await Lookup.SearchAsync(new SearchFeatureSubjectsInput
        {
            SubjectType = FeatureSubjectTypes.User,
            Filter = "carol",
            TenantId = ProgressiveDeliveryTestData.TenantA
        });
        tenantResult.Items.ShouldHaveSingleItem().SubjectId.ShouldBe(FakeExternalUserLookupServiceProvider.TenantUserId.ToString("D"));
    }

    [Test]
    public async Task Tenant_Caller_Cannot_Search_Another_Tenants_Users()
    {
        using (GetRequiredService<ICurrentTenant>().Change(ProgressiveDeliveryTestData.TenantB))
        {
            var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput
            {
                SubjectType = FeatureSubjectTypes.User,
                Filter = "carol",
                TenantId = ProgressiveDeliveryTestData.TenantA
            });

            result.Items.ShouldBeEmpty();
        }
    }

    [Test]
    public async Task Tenants_Are_Found_By_Name()
    {
        var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.Tenant, Filter = "Acme" });

        var hit = result.Items.ShouldHaveSingleItem();
        hit.SubjectId.ShouldBe(ProgressiveDeliveryTestData.TenantA.ToString("D"));
        hit.DisplayName.ShouldBe("acme-clinic");
    }

    [Test]
    public async Task Tenant_Caller_Only_Sees_Its_Own_Tenant()
    {
        using (GetRequiredService<ICurrentTenant>().Change(ProgressiveDeliveryTestData.TenantB))
        {
            var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.Tenant });

            result.Items.ShouldHaveSingleItem().DisplayName.ShouldBe("beta-hospital");
        }
    }

    [Test]
    public async Task Types_Without_A_Provider_Suggest_Already_Assigned_Ids()
    {
        await Override(FeatureSubjectTypes.Client, "cab-mobile");
        await Override(FeatureSubjectTypes.Client, "cab-desktop");

        var all = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.Client });
        all.Items.Select(i => i.SubjectId).ShouldBe(["cab-desktop", "cab-mobile"]);
        all.Items.ShouldAllBe(i => i.HasAssignments && i.DisplayName == i.SubjectId);

        var filtered = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.Client, Filter = "mob" });
        filtered.Items.ShouldHaveSingleItem().SubjectId.ShouldBe("cab-mobile");
    }

    [Test]
    public async Task Users_With_Assignments_Are_Flagged()
    {
        await Override(FeatureSubjectTypes.User, ProgressiveDeliveryTestData.UserId.ToString("D"));

        var result = await Lookup.SearchAsync(new SearchFeatureSubjectsInput { SubjectType = FeatureSubjectTypes.User });

        result.Items.Single(i => i.DisplayName == "alice").HasAssignments.ShouldBeTrue();
        result.Items.Single(i => i.DisplayName == "bob").HasAssignments.ShouldBeFalse();
    }

    [Test]
    public async Task Find_Returns_Display_Name_Or_Null()
    {
        var found = await Lookup.FindAsync(new SubjectRefDto { SubjectType = FeatureSubjectTypes.User, SubjectId = ProgressiveDeliveryTestData.UserId.ToString("D").ToUpperInvariant() });
        found.ShouldNotBeNull().DisplayName.ShouldBe("alice");

        var tenantUser = await Lookup.FindAsync(new SubjectRefDto
        {
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = FakeExternalUserLookupServiceProvider.TenantUserId.ToString("D"),
            TenantId = ProgressiveDeliveryTestData.TenantA,
            UseCurrentTenant = false
        });
        tenantUser.ShouldNotBeNull().DisplayName.ShouldBe("carol");

        (await Lookup.FindAsync(new SubjectRefDto { SubjectType = FeatureSubjectTypes.User, SubjectId = Guid.NewGuid().ToString() })).ShouldBeNull();
        (await Lookup.FindAsync(new SubjectRefDto { SubjectType = FeatureSubjectTypes.Client, SubjectId = "cab-mobile" })).ShouldBeNull();
    }

    [Test]
    public async Task Assignment_And_Transition_Listings_Carry_Display_Names()
    {
        await Override(FeatureSubjectTypes.User, ProgressiveDeliveryTestData.UserId.ToString("D"));
        await Override(FeatureSubjectTypes.Client, "cab-mobile");

        var assignments = (await Assignments.GetListAsync(new GetFeatureAssignmentsInput())).Items;
        assignments.Single(a => a.SubjectType == FeatureSubjectTypes.User).SubjectDisplayName.ShouldBe("alice");
        assignments.Single(a => a.SubjectType == FeatureSubjectTypes.Client).SubjectDisplayName.ShouldBeNull();

        var transitions = (await GetRequiredService<IFeatureTransitionAppService>().GetListAsync(new GetFeatureTransitionsInput
        {
            SubjectType = FeatureSubjectTypes.User
        })).Items;
        transitions.ShouldNotBeEmpty();
        transitions.ShouldAllBe(t => t.SubjectDisplayName == "alice");
    }

    private Task Override(string subjectType, string subjectId) => Assignments.OverrideAsync(new OverrideFeatureAssignmentDto
    {
        TrackName = Track,
        SubjectType = subjectType,
        SubjectId = subjectId,
        Level = 2,
        Reason = "test"
    });
}
