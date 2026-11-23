namespace HashForge.Mining.Shares;

/// <summary>Told about every block the node accepted.</summary>
public interface IBlockObserver
{
    Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken);
}
