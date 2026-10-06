using System.Net;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// One operation of the permission matrix: how to perform it, and who is allowed to.
/// </summary>
public sealed class AccessOperation
{
    public AccessOperation(
        string name,
        AccessArea area,
        AccessLevel required,
        HttpStatusCode successStatus,
        Func<ApiSession, ProjectScenario, Task<HttpResponseMessage>> execute)
    {
        Name = name;
        Area = area;
        Required = required;
        SuccessStatus = successStatus;
        Execute = execute;
    }

    public string Name { get; }

    public AccessArea Area { get; }

    public AccessLevel Required { get; }

    public HttpStatusCode SuccessStatus { get; }

    public Func<ApiSession, ProjectScenario, Task<HttpResponseMessage>> Execute { get; }

    public bool IsAllowedFor(Actor actor)
    {
        return actor switch
        {
            Actor.Admin => true,
            Actor.Owner => true,
            Actor.Editor => Required != AccessLevel.Manage,
            Actor.Viewer => Required == AccessLevel.View,
            _ => false
        };
    }

    public HttpStatusCode ExpectedFor(Actor actor)
    {
        if (actor == Actor.Anonymous)
        {
            return HttpStatusCode.Unauthorized;
        }

        return IsAllowedFor(actor) ? SuccessStatus : HttpStatusCode.Forbidden;
    }
}
