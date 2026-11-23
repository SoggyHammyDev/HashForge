using HashForge.Bitcoin.Rpc;

namespace HashForge.Mining.Jobs;

public interface ITemplateSink
{
    void Publish(BlockTemplate template);
}
