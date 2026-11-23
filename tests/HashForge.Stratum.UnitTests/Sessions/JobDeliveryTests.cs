using System.Text.Json;
using HashForge.Mining.Jobs;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.UnitTests.Sessions;

public sealed class JobDeliveryTests
{
    private const string TipA = "000000000000000000000000000000000000000000000000000000000000000a";
    private const string TipB = "000000000000000000000000000000000000000000000000000000000000000b";

    private readonly FakeConnection _connection = new();
    private readonly JobDelivery _delivery;

    public JobDeliveryTests() => _delivery = new JobDelivery(new MinerChannel(_connection, TimeSpan.FromSeconds(30)));

    private static MiningJob Job(string id, long sequence, string tip) =>
        HandlerTestData.Job(StratumTestKit.Pool(), id, sequence, tip);

    private IEnumerable<(string JobId, bool CleanJobs)> Sent() => _connection.Sent.Select(line =>
    {
        using var document = JsonDocument.Parse(line);
        var parameters = document.RootElement.GetProperty("params");
        return (parameters[0].GetString()!, parameters[8].GetBoolean());
    });

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_FirstJobIsSent_THEN_MinerIsToldToStartClean()
    {
        await _delivery.SendAsync(Job("1", 1, TipA), CancellationToken.None);

        Assert.Equal([("1", true)], Sent());
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_WorkIsRefreshedOnSameTip_THEN_MinerKeepsItsCurrentWork()
    {
        await _delivery.SendAsync(Job("1", 1, TipA), CancellationToken.None);
        await _delivery.SendAsync(Job("2", 2, TipA), CancellationToken.None);

        Assert.Equal([("1", true), ("2", false)], Sent());
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_ChainTipChanges_THEN_MinerIsToldToDropOldWork()
    {
        await _delivery.SendAsync(Job("1", 1, TipA), CancellationToken.None);
        await _delivery.SendAsync(Job("2", 2, TipB), CancellationToken.None);

        Assert.Equal([("1", true), ("2", true)], Sent());
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_OlderJobArrivesAfterNewerOne_THEN_OlderJobIsNeverSent()
    {
        await _delivery.SendAsync(Job("new", 5, TipB), CancellationToken.None);

        await _delivery.SendAsync(Job("old", 4, TipA), CancellationToken.None);

        Assert.Equal([("new", true)], Sent());
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_SameJobIsSentAgain_THEN_ItIsResentWithoutCleaning()
    {
        var job = Job("1", 1, TipA);

        await _delivery.SendAsync(job, CancellationToken.None);
        await _delivery.SendAsync(job, CancellationToken.None);

        Assert.Equal([("1", true), ("1", false)], Sent());
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_ManyJobsRaceInRandomOrder_THEN_MinerOnlyEverMovesForward()
    {
        var jobs = Enumerable.Range(1, 200).Select(i => Job(i.ToString("x"), i, i % 10 == 0 ? TipB : TipA)).ToList();

        await Task.WhenAll(jobs.OrderBy(_ => Random.Shared.Next()).Select(job => Task.Run(() => _delivery.SendAsync(job, CancellationToken.None))));

        var sequences = Sent().Select(s => Convert.ToInt64(s.JobId, 16)).ToList();
        Assert.Equal(sequences.Order(), sequences);
        Assert.Equal(200, sequences[^1]);
    }
}