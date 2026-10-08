namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// The users of one cross-user attack: who attacks, whose tasks are the target, and a third user who has
/// nothing to do with either and whose project the attacker may try to go through.
/// </summary>
public sealed class CrossUserContext
{
    public CrossUserContext(TaskOwner attacker, TaskOwner victim, TaskOwner third)
    {
        Attacker = attacker;
        Victim = victim;
        Third = third;
    }

    public TaskOwner Attacker { get; }

    public TaskOwner Victim { get; }

    public TaskOwner Third { get; }
}
