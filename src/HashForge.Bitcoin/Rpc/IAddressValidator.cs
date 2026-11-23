namespace HashForge.Bitcoin.Rpc;

public interface IAddressValidator
{
    Task<AddressValidation> ValidateAddressAsync(string address, CancellationToken cancellationToken);
}
