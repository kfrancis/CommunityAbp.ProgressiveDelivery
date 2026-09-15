using System.Diagnostics;
using System.Diagnostics.Metrics;
using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.OpenTelemetry;
using CommunityAbp.ProgressiveDelivery.Subjects;
using TUnit.Core;

namespace CommunityAbp.ProgressiveDelivery.Telemetry;

public class OpenTelemetry_Tests : ProgressiveDeliveryDomainTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;
    private static readonly Guid User = ProgressiveDeliveryTestData.UserId;

    private IProgressiveDelivery ProgressiveDelivery => GetRequiredService<IProgressiveDelivery>();

    [Test]
    public async Task Activity_Reports_Selected_Level_And_Fallback_Outcome()
    {
        await AssignAsync(Track, FeatureSubject.User(User), 3);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ProgressiveDeliveryInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (activities) { activities.Add(activity); } }
        };
        ActivitySource.AddActivityListener(listener);

        var measurements = new List<(string Instrument, object Value, Dictionary<string, object?> Tags)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ProgressiveDeliveryInstrumentation.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        meterListener.Start();

        using (ChangeUser(User))
        {
            var result = await ProgressiveDelivery.ExecuteAsync(Track, new ProgressiveRoutes<int>
            {
                [1] = _ => Task.FromResult(1),
                [2] = _ => Task.FromResult(2),
                [3] = _ => throw new TimeoutException("level 3 too slow")
            }, new ProgressiveExecutionOptions { OperationName = "LoadClaims" });

            result.ShouldBe(2);
        }

        var activity = activities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(ProgressiveDeliveryInstrumentation.ExecutionActivityName);
        activity.GetTagItem(ProgressiveDeliveryTagNames.Track).ShouldBe(Track);
        activity.GetTagItem(ProgressiveDeliveryTagNames.Level).ShouldBe(2);
        activity.GetTagItem(ProgressiveDeliveryTagNames.OfficialLevel).ShouldBe(1);
        activity.GetTagItem(ProgressiveDeliveryTagNames.Experimental).ShouldBe(true);
        activity.GetTagItem(ProgressiveDeliveryTagNames.Fallback).ShouldBe(true);
        activity.GetTagItem(ProgressiveDeliveryTagNames.FromLevel).ShouldBe(3);
        activity.GetTagItem(ProgressiveDeliveryTagNames.ToLevel).ShouldBe(2);
        activity.GetTagItem(ProgressiveDeliveryTagNames.Success).ShouldBe(true);
        activity.GetTagItem(ProgressiveDeliveryTagNames.SubjectType).ShouldBe(FeatureSubjectTypes.User);
        activity.GetTagItem(ProgressiveDeliveryTagNames.SubjectId).ShouldBeNull();
        activity.GetTagItem(ProgressiveDeliveryTagNames.Application).ShouldBe("DomainTests");
        activity.GetTagItem(ProgressiveDeliveryTagNames.Operation).ShouldBe("LoadClaims");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.Events.ShouldContain(e => e.Name == "progressive_delivery.fallback");
        activity.Events.ShouldContain(e => e.Name == "exception");

        lock (measurements)
        {
            measurements.Count(m => m.Instrument == "progressive_delivery.execution.count").ShouldBe(2);
            measurements.Count(m => m.Instrument == "progressive_delivery.execution.errors").ShouldBe(1);
            measurements.Count(m => m.Instrument == "progressive_delivery.fallback.count").ShouldBe(1);
            measurements.Count(m => m.Instrument == "progressive_delivery.demotion.count").ShouldBe(1);
            measurements.Count(m => m.Instrument == "progressive_delivery.execution.duration").ShouldBe(2);

            var success = measurements.Single(m => m.Instrument == "progressive_delivery.execution.count" && Equals(m.Tags[ProgressiveDeliveryTagNames.Success], true));
            success.Tags[ProgressiveDeliveryTagNames.Level].ShouldBe(2);
            success.Tags[ProgressiveDeliveryTagNames.Fallback].ShouldBe(true);
        }

        void Record(Instrument instrument, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value;
            }

            lock (measurements)
            {
                measurements.Add((instrument.Name, value, dict));
            }
        }
    }
}
