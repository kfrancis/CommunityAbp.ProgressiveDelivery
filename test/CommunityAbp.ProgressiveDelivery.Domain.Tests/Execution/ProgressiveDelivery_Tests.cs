using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Transitions;
using TUnit.Core;
using Volo.Abp.Tracing;

namespace CommunityAbp.ProgressiveDelivery.Execution;

public class ProgressiveDelivery_Tests : ProgressiveDeliveryDomainTestBase
{
    private static readonly Guid User = ProgressiveDeliveryTestData.UserId;
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;

    private IProgressiveDelivery ProgressiveDelivery => GetRequiredService<IProgressiveDelivery>();

    [Test]
    public async Task Executes_Highest_Route_At_Or_Below_Effective_Level()
    {
        await AssignAsync(Track, FeatureSubject.User(User), 3);

        using (ChangeUser(User))
        {
            var result = await ProgressiveDelivery.ExecuteAsync(Track, new ProgressiveRoutes<string>
            {
                [0] = _ => Task.FromResult("legacy"),
                [2] = _ => Task.FromResult("cached")
            });

            result.ShouldBe("cached");
        }
    }

    [Test]
    public async Task Successful_Experimental_Execution_Does_Not_Demote()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);

        using (ChangeUser(User))
        {
            var result = await ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [3]));
            result.ShouldBe(3);
        }

        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(3);
        Telemetry.Fallbacks.ShouldBeEmpty();
        Telemetry.Transitions.ShouldNotContain(t => t.TransitionType == FeatureTransitionType.AutomaticDemotion);
    }

    [Test]
    public async Task Failed_SafeRead_Execution_Demotes_And_Retries_Lower_Route()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);

        using (ChangeUser(User))
        {
            var result = await ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [2, 1, 0]));
            result.ShouldBe(2);
        }

        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(2);
        Telemetry.Fallbacks.ShouldHaveSingleItem().ShouldBe((Track, 3, 2));

        var demotion = Telemetry.Transitions.Where(t => t.TransitionType == FeatureTransitionType.AutomaticDemotion).ShouldHaveSingleItem();
        demotion.FromLevel.ShouldBe(3);
        demotion.ToLevel.ShouldBe(2);

        // Next request runs level 2 directly.
        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(2);
        }
    }

    [Test]
    public async Task Non_Retryable_Policy_Does_Not_Retry_And_Surfaces_Exception()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);
        var executed = new List<int>();

        using (ChangeUser(User))
        {
            var ex = await Should.ThrowAsync<InvalidOperationException>(() => ProgressiveDelivery.ExecuteAsync(
                Track,
                Routes(succeedAt: [2, 1, 0], executed),
                new ProgressiveExecutionOptions { FallbackPolicy = FallbackPolicy.None }));

            ex.Data[ProgressiveDeliveryTagNames.Track].ShouldBe(Track);
            ex.Data[ProgressiveDeliveryTagNames.Level].ShouldBe(3);
            ex.Data[ProgressiveDeliveryTagNames.AttemptedLevels].ShouldBe("3");
        }

        executed.ShouldBe([3]);
        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(3);
        Telemetry.Fallbacks.ShouldBeEmpty();
    }

    [Test]
    public async Task DemoteOnly_Policy_Demotes_But_Does_Not_Retry()
    {
        // Search track: level 2 is DemoteOnly.
        var subject = FeatureSubject.User(User);
        await AssignAsync(ProgressiveDeliveryTestData.SearchTrack, subject, 2);
        var executed = new List<int>();

        using (ChangeUser(User))
        {
            await Should.ThrowAsync<InvalidOperationException>(() => ProgressiveDelivery.ExecuteAsync(
                ProgressiveDeliveryTestData.SearchTrack,
                Routes(succeedAt: [1, 0], executed)));
        }

        executed.ShouldBe([2]);
        (await FindAssignmentAsync(ProgressiveDeliveryTestData.SearchTrack, subject))!.AssignedLevel.ShouldBe(1);
        Telemetry.Transitions.Count(t => t.TransitionType == FeatureTransitionType.AutomaticDemotion).ShouldBe(1);
    }

    [Test]
    public async Task Multiple_Fallback_Failures_Reach_Lower_Routes()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);
        var executed = new List<int>();

        using (ChangeUser(User))
        {
            var result = await ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [1, 0], executed));
            result.ShouldBe(1);
        }

        executed.ShouldBe([3, 2, 1]);
        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(1);
        Telemetry.Fallbacks.ShouldBe([(Track, 3, 2), (Track, 2, 1)]);
        Telemetry.Transitions.Count(t => t.TransitionType == FeatureTransitionType.AutomaticDemotion).ShouldBe(2);
    }

    [Test]
    public async Task Fallback_Never_Goes_Below_Official_Level()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);
        var executed = new List<int>();

        using (ChangeUser(User))
        {
            var ex = await Should.ThrowAsync<InvalidOperationException>(() => ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [0], executed)));

            ex.Data[ProgressiveDeliveryTagNames.AttemptedLevels].ShouldBe("3,2,1");
            ((string[])ex.Data[ProgressiveDeliveryTagNames.Prefix + "previous_failures"]!).Length.ShouldBe(2);
        }

        // Level 1 is official: it fails, nothing lower is tried, and the assignment rests at the official level.
        executed.ShouldBe([3, 2, 1]);
        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(1);
    }

    [Test]
    public async Task Missing_Route_Fails_Predictably()
    {
        using (ChangeUser(User))
        {
            var ex = await Should.ThrowAsync<ProgressiveRouteNotFoundException>(() => ProgressiveDelivery.ExecuteAsync(Track, new ProgressiveRoutes<int>
            {
                [5] = _ => Task.FromResult(5)
            }));

            ex.TrackName.ShouldBe(Track);
            ex.EffectiveLevel.ShouldBe(1);
            ex.AvailableRouteLevels.ShouldBe([5]);

            await Should.ThrowAsync<ArgumentException>(() => ProgressiveDelivery.ExecuteAsync(Track, new ProgressiveRoutes<int>()));
        }
    }

    [Test]
    public async Task Cancellation_Does_Not_Trigger_Fallback()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);
        using var cts = new CancellationTokenSource();

        using (ChangeUser(User))
        {
            await Should.ThrowAsync<OperationCanceledException>(() => ProgressiveDelivery.ExecuteAsync(Track, new ProgressiveRoutes<int>
            {
                [0] = _ => Task.FromResult(0),
                [3] = ct =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(3);
                }
            }, cancellationToken: cts.Token));
        }

        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(3);
        Telemetry.Fallbacks.ShouldBeEmpty();
    }

    [Test]
    public async Task Execution_At_Official_Level_Never_Falls_Below_It_Even_When_Policy_Allows()
    {
        // No current user: effective = official (1). Even with SafeRead forced, nothing below official is tried.
        var executed = new List<int>();

        await Should.ThrowAsync<InvalidOperationException>(() => ProgressiveDelivery.ExecuteAsync(
            Track,
            Routes(succeedAt: [0], executed),
            new ProgressiveExecutionOptions { FallbackPolicy = FallbackPolicy.SafeRead }));

        executed.ShouldBe([1]);
        Telemetry.Fallbacks.ShouldBeEmpty();
        Telemetry.Transitions.ShouldBeEmpty();
    }

    [Test]
    public async Task Demotion_Transition_Carries_Diagnostic_Metadata()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);

        using (ChangeUser(User))
        using (GetRequiredService<ICorrelationIdProvider>().Change("corr-42"))
        {
            await ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [2]), new ProgressiveExecutionOptions { OperationName = "LoadClaims" });
        }

        var transitions = await WithUnitOfWorkAsync(() => GetRequiredService<IFeatureTransitionRepository>().GetListAsync(subjectType: subject.Type, subjectId: subject.Id, transitionType: FeatureTransitionType.AutomaticDemotion));
        var demotion = transitions.ShouldHaveSingleItem();

        demotion.TrackName.ShouldBe(Track);
        demotion.FromLevel.ShouldBe(3);
        demotion.ToLevel.ShouldBe(2);
        demotion.Reason.ShouldNotBeNull().ShouldContain("InvalidOperationException");
        demotion.Metadata.ShouldNotBeNull();
        demotion.Metadata.ShouldContain("\"operation\":\"LoadClaims\"");
        demotion.Metadata.ShouldContain("\"application\":\"DomainTests\"");
        demotion.CorrelationId.ShouldBe("corr-42");
    }

    [Test]
    public async Task Demotion_Inside_Ambient_Unit_Of_Work_Is_Persisted_After_It_Completes()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(Track, subject, 3);

        using (ChangeUser(User))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var result = await ProgressiveDelivery.ExecuteAsync(Track, Routes(succeedAt: [2, 1, 0]));
                result.ShouldBe(2);

                // Deferred: the caller's unit of work still owns the connection.
                (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(3);
            });
        }

        // The write runs on the thread pool once the unit of work is disposed. Wait for it rather than polling the
        // database: the test database is a single shared SQLite connection that cannot be used concurrently.
        await DeferredWrites.WaitForIdleAsync();

        (await FindAssignmentAsync(Track, subject))!.AssignedLevel.ShouldBe(2);
        Telemetry.Transitions.ShouldContain(t => t.TransitionType == FeatureTransitionType.AutomaticDemotion && t.ToLevel == 2);
    }

    [Test]
    public async Task Demotion_Survives_A_Failing_Caller_Unit_Of_Work()
    {
        // Search track: level 2 is DemoteOnly, so the exception propagates and the caller's unit of work is rolled back.
        var subject = FeatureSubject.User(User);
        await AssignAsync(ProgressiveDeliveryTestData.SearchTrack, subject, 2);

        using (ChangeUser(User))
        {
            await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(() =>
                ProgressiveDelivery.ExecuteAsync(ProgressiveDeliveryTestData.SearchTrack, Routes(succeedAt: [1, 0]))));
        }

        await DeferredWrites.WaitForIdleAsync();

        (await FindAssignmentAsync(ProgressiveDeliveryTestData.SearchTrack, subject))!.AssignedLevel.ShouldBe(1);
    }

    /// <summary>Routes 0..3 returning their level; every level not in <paramref name="succeedAt"/> throws.</summary>
    private static ProgressiveRoutes<int> Routes(int[] succeedAt, List<int>? executed = null)
    {
        var routes = new ProgressiveRoutes<int>();
        for (var level = 0; level <= 3; level++)
        {
            var captured = level;
            routes[level] = _ =>
            {
                executed?.Add(captured);
                return succeedAt.Contains(captured)
                    ? Task.FromResult(captured)
                    : throw new InvalidOperationException($"Level {captured} failed");
            };
        }

        return routes;
    }
}
