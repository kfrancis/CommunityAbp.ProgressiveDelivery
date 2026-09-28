using CommunityAbp.ProgressiveDelivery.Caching;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using TUnit.Core;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

public class FeatureSubjectNormalization_Tests : ProgressiveDeliveryDomainTestBase
{
    private static readonly Guid User = ProgressiveDeliveryTestData.HexUserId;
    private static readonly string Canonical = User.ToString("D");

    [Test]
    [Arguments("A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D")]
    [Arguments("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d")]
    [Arguments("{A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D}")]
    [Arguments("(a1b2c3d4-E5F6-4a7b-8c9d-0e1f2a3b4c5d)")]
    [Arguments("  A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D\t")]
    public async Task Guid_Ids_Are_Canonicalised_To_Lower_Case_D_Format(string supplied)
    {
        var subject = new FeatureSubject("user", supplied);

        subject.ShouldBe(FeatureSubject.User(User));
        subject.Id.ShouldBe(Canonical);
        subject.Type.ShouldBe(FeatureSubjectTypes.User);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Non_Guid_Ids_And_Custom_Types_Are_Only_Trimmed()
    {
        var subject = FeatureSubject.Custom(" Device ", " ABC-def ");

        subject.Type.ShouldBe("Device");
        subject.Id.ShouldBe("ABC-def");

        // "N" format is indistinguishable from an arbitrary 32-char hex key, so it is left alone.
        FeatureSubject.NormalizeId("A1B2C3D4E5F64A7B8C9D0E1F2A3B4C5D").ShouldBe("A1B2C3D4E5F64A7B8C9D0E1F2A3B4C5D");
        FeatureSubject.NormalizeId(null).ShouldBeNull();
        FeatureSubject.NormalizeType(null).ShouldBeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rollout_Bucket_Does_Not_Depend_On_Guid_Casing()
    {
        var allocator = GetRequiredService<IRolloutCohortAllocator>();

        var runtime = allocator.GetBucket(FeatureSubject.User(User), ProgressiveDeliveryTestData.ClaimsTrack, 2);
        var pasted = allocator.GetBucket(new FeatureSubject("USER", Canonical.ToUpperInvariant()), ProgressiveDeliveryTestData.ClaimsTrack, 2);

        pasted.ShouldBe(runtime);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cache_Key_Does_Not_Depend_On_Guid_Casing_But_Keeps_Distinct_Custom_Types_Apart()
    {
        var track = Guid.NewGuid();

        IFeatureAssignmentCache.BuildKey(track, "user", Canonical.ToUpperInvariant())
            .ShouldBe(IFeatureAssignmentCache.BuildKey(track, FeatureSubjectTypes.User, Canonical));

        // The repository compares custom types ordinally, so the cache must too.
        IFeatureAssignmentCache.BuildKey(track, "device", "1")
            .ShouldNotBe(IFeatureAssignmentCache.BuildKey(track, "Device", "1"));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pasted_Upper_Case_Id_Resolves_The_Runtime_Assignment()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 3);

        var found = await FindAssignmentAsync(ProgressiveDeliveryTestData.ClaimsTrack, new FeatureSubject("User", Canonical.ToUpperInvariant()));

        found.ShouldNotBeNull().AssignedLevel.ShouldBe(3);
    }
}
