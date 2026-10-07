using CommunityAbp.ProgressiveDelivery.OpenIddict;
using NSubstitute;
using TUnit.Core;
using Volo.Abp.OpenIddict.Applications;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

public class OpenIddictClientSubjectLookupProvider_Tests
{
    private readonly IOpenIddictApplicationRepository _repository = Substitute.For<IOpenIddictApplicationRepository>();

    private OpenIddictClientSubjectLookupProvider Provider => new(_repository);

    public OpenIddictClientSubjectLookupProvider_Tests()
    {
        List<OpenIddictApplication> applications =
        [
            App("cab-mobile", "CabMD Mobile", "public", "native"),
            App("cab-web", "CabMD Web", "confidential", "web"),
            App("billing-worker", null, "confidential", null)
        ];

        _repository.GetListAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(applications);
        _repository.FindByClientIdAsync("cab-web", Arg.Any<CancellationToken>()).Returns(applications[1]);
    }

    [Test]
    [Arguments("mobile")]
    [Arguments("CABMD MOB")]
    public async Task Finds_Applications_By_Client_Id_Or_Display_Name(string filter)
    {
        var result = await Provider.SearchAsync(filter, 10);

        var hit = result.ShouldHaveSingleItem();
        hit.SubjectId.ShouldBe("cab-mobile");
        hit.DisplayName.ShouldBe("CabMD Mobile");
        hit.Detail.ShouldBe("public · native");
    }

    [Test]
    public async Task Empty_Filter_Lists_Applications_Up_To_The_Limit()
    {
        (await Provider.SearchAsync(null, 10)).Count.ShouldBe(3);
        (await Provider.SearchAsync("  ", 2)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Application_Without_Display_Name_Is_Labelled_By_Client_Id()
    {
        var hit = (await Provider.SearchAsync("billing", 10)).ShouldHaveSingleItem();

        hit.DisplayName.ShouldBe("billing-worker");
        hit.Detail.ShouldBe("confidential");
    }

    [Test]
    public async Task Find_Resolves_By_Client_Id()
    {
        Provider.SubjectType.ShouldBe(FeatureSubjectTypes.Client);
        (await Provider.FindAsync("cab-web")).ShouldNotBeNull().DisplayName.ShouldBe("CabMD Web");
        (await Provider.FindAsync("unknown")).ShouldBeNull();
    }

    private static OpenIddictApplication App(string clientId, string? displayName, string? clientType, string? applicationType)
        => new(Guid.NewGuid())
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientType = clientType,
            ApplicationType = applicationType
        };
}
