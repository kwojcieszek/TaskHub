using System.Net;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// One way a user may try to reach the tasks of another user, and the answer the API must give.
/// </summary>
public sealed class CrossUserAttack
{
    public CrossUserAttack(string name, HttpStatusCode expected, Func<CrossUserContext, Task<HttpResponseMessage>> execute, string? expectedBody = null)
    {
        Name = name;
        Expected = expected;
        Execute = execute;
        ExpectedBody = expectedBody;
    }

    public string Name { get; }

    public HttpStatusCode Expected { get; }

    public Func<CrossUserContext, Task<HttpResponseMessage>> Execute { get; }

    // Whether the attempt uses anything of the third user; if not, who that user is makes no difference.
    public bool InvolvesThird => Name.Contains(CrossUserAttacks.ThirdSource);

    // Set for the few attempts that are answered normally, but must come back empty.
    public string? ExpectedBody { get; }
}
