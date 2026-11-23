using System.Net;

namespace HashForge.Web.Services;

public sealed record ConnectedAddress(IPAddress Address, int Connections);
