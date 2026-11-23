using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;

namespace HashForge.Stratum.UnitTests;

internal static class HandlerTestData
{
    public static MiningJob Job(PoolDefinition pool, string id = "a1")
    {
        var template = new BlockTemplate
        {
            Version = 0x20000000,
            PreviousBlockHash = new string('0', 63) + "1",
            Bits = "1d00ffff",
            CoinbaseValue = 312_500_000,
            CurrentTime = 1_700_000_000,
            Height = 850_000,
        };
        return new MiningJob(id, pool, template, new CoinbaseTransaction([0x01], [0x02], 12, false), []);
    }
}
