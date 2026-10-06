namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Defects of the API these tests found and that are not fixed yet. A test that proves one is skipped with
/// the reason below, so the suite stays green while every run still lists what is open.
/// Remove the Skip from a test to reproduce its defect; delete the entry here once it is fixed.
/// </summary>
public static class KnownDefects
{
    private const string Prefix = "Known defect: ";

    public const string MissingUserCausesServerError =
        Prefix + "adding a project member or a user group member who does not exist ends in 500 instead of a 4xx answer (foreign key violation is not handled).";

    public const string DeletingUserWithOwnedDataCausesServerError =
        Prefix + "deleting a user who owns a project or project group, or who uploaded an attachment, ends in 500 instead of a clear refusal or a clean removal.";

    public const string MissingUserGroupIsNotReported =
        Prefix + "listing the members of a user group that does not exist returns an empty list instead of 404.";
}
