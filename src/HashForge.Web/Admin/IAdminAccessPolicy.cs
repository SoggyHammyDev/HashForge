using System.Net;

namespace HashForge.Web.Admin;

public interface IAdminAccessPolicy
{
    bool IsAllowed(IPAddress? remoteAddress);
}
