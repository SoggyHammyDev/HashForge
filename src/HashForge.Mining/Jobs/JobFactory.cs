using System.Text;
using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Payouts;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Jobs;

/// <summary>Turns a block template into a job whose coinbase pays the pool's miners directly.</summary>
public sealed class JobFactory(
    IPayoutPlanSource planSource,
    IAddressScriptResolver addressResolver,
    IOptions<MiningOptions> options) : IJobFactory
{
    public const int Extranonce1Size = 4;

    public async Task<MiningJob> CreateAsync(
        string jobId,
        PoolDefinition pool,
        BlockTemplate template,
        string minerAddress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(template);

        var plan = planSource.GetPlan(pool, minerAddress);
        var payouts = PayoutDistributor.Distribute(plan.Weights, template.CoinbaseValue, pool.ToPayoutRules(), plan.FinderBonus);
        var outputs = new List<TransactionOutput>(payouts.Count);
        foreach (var payout in payouts)
        {
            outputs.Add(new TransactionOutput(payout.Satoshis, await ResolveScriptAsync(payout.Address, cancellationToken).ConfigureAwait(false)));
        }

        var settings = options.Value;
        var coinbase = CoinbaseBuilder.Build(new CoinbaseSpec(
            template.Height,
            Encoding.UTF8.GetBytes(settings.CoinbaseTag),
            Extranonce1Size + settings.Extranonce2Size,
            outputs,
            template.DefaultWitnessCommitment is null ? null : Convert.FromHexString(template.DefaultWitnessCommitment)));

        // The fee address counts as a miner only when it also mined; otherwise its output is purely the pool fee.
        var minersPaid = payouts.Count(p => p.Address != pool.FeeAddress || plan.Weights.GetValueOrDefault(pool.FeeAddress) > 0);
        return new MiningJob(jobId, pool, template, coinbase, payouts, minersPaid);
    }

    private async Task<byte[]> ResolveScriptAsync(string address, CancellationToken cancellationToken)
    {
        var resolution = await addressResolver.ResolveAsync(address, cancellationToken).ConfigureAwait(false);
        return resolution.ScriptPubKey
            ?? throw new InvalidOperationException($"Cannot pay address '{address}': {resolution.Error}");
    }
}
