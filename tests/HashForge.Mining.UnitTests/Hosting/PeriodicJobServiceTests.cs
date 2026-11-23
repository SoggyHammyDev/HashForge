using HashForge.Mining.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Hosting;

public sealed class PeriodicJobServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time = new(TestData.Now);

    private sealed class CountingJob(TimeSpan interval, bool failFirst = false) : IPeriodicJob
    {
        private readonly System.Threading.Channels.Channel<int> _ran = System.Threading.Channels.Channel.CreateUnbounded<int>();
        private int _runs;

        public string Name => "counting";

        public TimeSpan Interval { get; } = interval;

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var run = Interlocked.Increment(ref _runs);
            _ran.Writer.TryWrite(run);
            return failFirst && run == 1 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }

        public async Task WaitForRunAsync() => await _ran.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        public int Runs => Volatile.Read(ref _runs);
    }

    private PeriodicJobService Service(params IPeriodicJob[] jobs) => new(jobs, _time, NullLogger<PeriodicJobService>.Instance);

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_ServiceStarts_THEN_EachJobRunsImmediately()
    {
        var first = new CountingJob(TimeSpan.FromMinutes(1));
        var second = new CountingJob(TimeSpan.FromMinutes(5));
        using var service = Service(first, second);

        await service.StartAsync(CancellationToken.None);
        await first.WaitForRunAsync();
        await second.WaitForRunAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, first.Runs);
        Assert.Equal(1, second.Runs);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_IntervalElapses_THEN_JobRunsAgain()
    {
        var job = new CountingJob(TimeSpan.FromMinutes(1));
        using var service = Service(job);
        await service.StartAsync(CancellationToken.None);
        await job.WaitForRunAsync();

        _time.Advance(TimeSpan.FromMinutes(1));
        await job.WaitForRunAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, job.Runs);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_JobThrows_THEN_ItIsRetriedOnNextTick()
    {
        var job = new CountingJob(TimeSpan.FromMinutes(1), failFirst: true);
        using var service = Service(job);
        await service.StartAsync(CancellationToken.None);
        await job.WaitForRunAsync();

        _time.Advance(TimeSpan.FromMinutes(1));
        await job.WaitForRunAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, job.Runs);
    }

    [Fact]
    public async Task GIVEN_StopAsync_WHEN_JobsAreWaiting_THEN_ServiceStopsCleanly()
    {
        var job = new CountingJob(TimeSpan.FromHours(1));
        using var service = Service(job);
        await service.StartAsync(CancellationToken.None);
        await job.WaitForRunAsync();

        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }
}
