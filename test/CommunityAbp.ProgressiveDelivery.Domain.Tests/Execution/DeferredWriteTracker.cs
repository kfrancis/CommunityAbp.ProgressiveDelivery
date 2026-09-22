namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// Counts side-effect writes scheduled through <see cref="TrackingWriteScheduler"/> so tests can wait for deferred
/// work deterministically instead of polling the database. Polling the shared in-memory SQLite connection while the
/// deferred write runs on the thread pool races on the connection and makes the write fail intermittently.
/// </summary>
public sealed class DeferredWriteTracker
{
    private int _scheduled;
    private int _completed;

    public int Scheduled => Volatile.Read(ref _scheduled);

    public int Completed => Volatile.Read(ref _completed);

    public void OnScheduled() => Interlocked.Increment(ref _scheduled);

    public void OnCompleted() => Interlocked.Increment(ref _completed);

    /// <summary>Waits until every scheduled write has finished (successfully or not).</summary>
    public async Task WaitForIdleAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (Completed < Scheduled)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Deferred writes did not finish in time ({Completed}/{Scheduled} completed).");
            }

            await Task.Delay(10);
        }
    }
}
