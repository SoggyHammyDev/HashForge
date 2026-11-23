namespace HashForge.Mining.Payouts;

public interface IAddressScriptResolver
{
    Task<AddressResolution> ResolveAsync(string address, CancellationToken cancellationToken);
}
