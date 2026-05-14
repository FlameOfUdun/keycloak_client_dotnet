using Microsoft.AspNetCore.Authorization;

namespace Winche.KeycloakClient.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ProtectedResourceAttribute : AuthorizeAttribute
{
    public ProtectedResourceAttribute(string resource, string scope)
    {
        Resource = resource;
        Scope = scope;
        Policy = $"kc:protected:{resource}:{scope}";
    }

    public string Resource { get; }
    public string Scope { get; }
}
