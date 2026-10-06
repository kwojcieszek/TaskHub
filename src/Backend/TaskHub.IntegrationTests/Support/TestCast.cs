namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// The callers a permission test plays against each other. They are created once per API instance
/// and reused, because every test builds its own project for them.
/// </summary>
public sealed class TestCast
{
    public TestCast(ApiSession anonymous, ApiSession admin, ApiSession owner, ApiSession editor, ApiSession viewer, ApiSession stranger)
    {
        Anonymous = anonymous;
        Admin = admin;
        Owner = owner;
        Editor = editor;
        Viewer = viewer;
        Stranger = stranger;
    }

    public ApiSession Anonymous { get; }

    public ApiSession Admin { get; }

    public ApiSession Owner { get; }

    public ApiSession Editor { get; }

    public ApiSession Viewer { get; }

    // Signed in, but not a member of any project the owner creates.
    public ApiSession Stranger { get; }

    public ApiSession For(Actor actor)
    {
        return actor switch
        {
            Actor.Anonymous => Anonymous,
            Actor.Stranger => Stranger,
            Actor.Viewer => Viewer,
            Actor.Editor => Editor,
            Actor.Owner => Owner,
            Actor.Admin => Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null)
        };
    }
}
