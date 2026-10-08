using System.Net;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Every attempt of one user on the tasks of another, built as combinations: each operation is tried with every
/// mix of identifiers in its address and body, taken from the attacker, the victim, a third user, or made up.
/// Only the combination that uses the attacker's own identifiers throughout is left out, because it is allowed.
/// </summary>
public static class CrossUserAttacks
{
    private const string AttackerSource = "Attacker";
    public const string ThirdSource = "Third";

    private const string MissingSource = "Missing";
    private const string EmptyList = "[]";

    // Where the project in the address comes from.
    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> Projects = new List<(string, Func<CrossUserContext, Guid>)>
    {
        (AttackerSource, context => context.Attacker.Project.Id),
        ("Victim", context => context.Victim.Project.Id),
        (ThirdSource, context => context.Third.Project.Id),
        (MissingSource, _ => Guid.NewGuid())
    };

    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> Tasks = new List<(string, Func<CrossUserContext, Guid>)>
    {
        (AttackerSource, context => context.Attacker.OwnTask.Id),
        ("Victim.Parent", context => context.Victim.ParentTask.Id),
        ("Victim.Sub", context => context.Victim.SubTask.Id),
        ("Victim.Own", context => context.Victim.OwnTask.Id),
        (MissingSource, _ => Guid.NewGuid())
    };

    // The attachments hang on the own task of each user, so only that task is combined with them.
    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> AttachmentTasks = new List<(string, Func<CrossUserContext, Guid>)>
    {
        (AttackerSource, context => context.Attacker.OwnTask.Id),
        ("Victim.Own", context => context.Victim.OwnTask.Id),
        (MissingSource, _ => Guid.NewGuid())
    };

    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> Attachments = new List<(string, Func<CrossUserContext, Guid>)>
    {
        (AttackerSource, context => context.Attacker.Attachment.Id),
        ("Victim", context => context.Victim.Attachment.Id),
        (MissingSource, _ => Guid.NewGuid())
    };

    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, string> Resolve)> Hashes = new List<(string, Func<CrossUserContext, string>)>
    {
        (AttackerSource, context => context.Attacker.Attachment.Sha256),
        ("Victim", context => context.Victim.Attachment.Sha256),
        (MissingSource, _ => $"{Guid.NewGuid():N}{Guid.NewGuid():N}")
    };

    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> Comments = new List<(string, Func<CrossUserContext, Guid>)>
    {
        (AttackerSource, context => context.Attacker.TaskComment.Id),
        ("Victim.Project", context => context.Victim.ProjectComment.Id),
        ("Victim.Task", context => context.Victim.TaskComment.Id),
        (MissingSource, _ => Guid.NewGuid())
    };

    // Tasks that are not the attacker's, to be named as the parent of one of the attacker's tasks.
    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> ForeignParents = Tasks.Where(task => task.Label != AttackerSource).ToList();

    // Users who do not belong to the attacker's project, to be named as the assignee of one of its tasks.
    private static readonly IReadOnlyList<(string Label, Func<CrossUserContext, Guid> Resolve)> ForeignUsers = new List<(string, Func<CrossUserContext, Guid>)>
    {
        ("Victim", context => context.Victim.Session.UserId),
        (ThirdSource, context => context.Third.Session.UserId),
        (MissingSource, _ => Guid.NewGuid())
    };

    public static readonly IReadOnlyList<CrossUserAttack> All = Build();

    public static CrossUserAttack Get(string name)
    {
        return All.Single(attack => attack.Name == name);
    }

    public static TheoryData<string> Cases()
    {
        var cases = new TheoryData<string>();

        foreach (var attack in All)
        {
            cases.Add(attack.Name);
        }

        return cases;
    }

    private static List<CrossUserAttack> Build()
    {
        var attacks = new List<CrossUserAttack>();

        AddProjectAttacks(attacks);
        AddTaskAttacks(attacks);
        AddAttachmentAttacks(attacks);
        AddCommentAttacks(attacks);
        AddHashAttacks(attacks);
        AddPlantingAttacks(attacks);

        return attacks;
    }

    // Operations addressed by a project alone. A project the attacker does not belong to is forbidden,
    // and one that does not exist is answered the same, so the answer does not tell which identifiers exist.
    private static void AddProjectAttacks(List<CrossUserAttack> attacks)
    {
        var operations = new List<(string Name, Func<ApiSession, CrossUserContext, Guid, Task<HttpResponseMessage>> Execute)>
        {
            ("ReadProject", (caller, _, project) => caller.GetAsync(ApiRoutes.Project(project))),
            ("ReadMembers", (caller, _, project) => caller.GetAsync(ApiRoutes.ProjectMembers(project))),
            ("UpdateProject", (caller, _, project) => caller.PutAsync(ApiRoutes.Project(project), new UpdateProjectRequest { Name = "Hijacked", IsActive = true })),
            ("DeactivateProject", (caller, _, project) => caller.PutAsync(ApiRoutes.Project(project), new UpdateProjectRequest { Name = "Hijacked", IsActive = false })),
            ("SetProjectGroup", (caller, _, project) => caller.PutAsync(ApiRoutes.ProjectGroupOf(project), new SetProjectGroupRequest { ProjectGroupId = null })),
            ("DeleteProject", (caller, _, project) => caller.DeleteAsync(ApiRoutes.Project(project))),
            ("JoinAsOwner", (caller, context, project) => caller.PostAsync(ApiRoutes.ProjectMembers(project), new AddProjectMemberRequest { UserId = context.Attacker.Session.UserId, Role = ProjectMemberRole.Owner })),
            ("JoinAsViewer", (caller, context, project) => caller.PostAsync(ApiRoutes.ProjectMembers(project), new AddProjectMemberRequest { UserId = context.Attacker.Session.UserId, Role = ProjectMemberRole.Viewer })),
            ("RemoveOwner", (caller, context, project) => caller.DeleteAsync(ApiRoutes.ProjectMember(project, context.Victim.Session.UserId))),
            ("ListTasks", (caller, _, project) => caller.GetAsync(ApiRoutes.Tasks(project))),
            ("CreateTask", (caller, _, project) => caller.PostAsync(ApiRoutes.Tasks(project), new CreateTaskItemRequest { Title = "Planted" })),
            ("CreateTaskForSelf", (caller, context, project) => caller.PostAsync(ApiRoutes.Tasks(project), new CreateTaskItemRequest { Title = "Planted", AssignedUserId = context.Attacker.Session.UserId })),
            ("ReadProjectComments", (caller, _, project) => caller.GetAsync(ApiRoutes.ProjectComments(project))),
            ("AddProjectComment", (caller, _, project) => caller.PostAsync(ApiRoutes.ProjectComments(project), new SaveCommentRequest { Content = "Planted" })),
            ("ReadTeams", (caller, _, project) => caller.GetAsync(ApiRoutes.ProjectTeams(project))),
            ("ReadAvailableTeams", (caller, _, project) => caller.GetAsync(ApiRoutes.AvailableProjectTeams(project))),
            ("AddTeam", (caller, _, project) => caller.PostAsync(ApiRoutes.ProjectTeams(project), new AddProjectTeamRequest { UserGroupId = Guid.NewGuid(), Role = ProjectMemberRole.Editor })),
            ("RemoveTeam", (caller, _, project) => caller.DeleteAsync(ApiRoutes.ProjectTeam(project, Guid.NewGuid())))
        };

        foreach (var operation in operations)
        {
            foreach (var project in Projects.Where(project => project.Label != AttackerSource))
            {
                attacks.Add(new CrossUserAttack(
                    $"{operation.Name}(project: {project.Label})",
                    HttpStatusCode.Forbidden,
                    context => operation.Execute(context.Attacker.Session, context, project.Resolve(context))));
            }
        }
    }

    // Operations addressed by a project and a task. A foreign project is forbidden whatever the task is;
    // a foreign task named inside the attacker's own project is simply not there.
    private static void AddTaskAttacks(List<CrossUserAttack> attacks)
    {
        var operations = new List<(string Name, Func<ApiSession, CrossUserContext, Guid, Guid, Task<HttpResponseMessage>> Execute)>
        {
            ("ReadTask", (caller, _, project, task) => caller.GetAsync(ApiRoutes.Task(project, task))),
            ("ReadSubTasks", (caller, _, project, task) => caller.GetAsync(ApiRoutes.SubTasks(project, task))),
            ("CreateSubTask", (caller, _, project, task) => caller.PostAsync(ApiRoutes.SubTasks(project, task), new CreateTaskItemRequest { Title = "Planted" })),
            ("UpdateTask", (caller, _, project, task) => caller.PutAsync(ApiRoutes.Task(project, task), new UpdateTaskItemRequest { Title = "Hijacked", Status = TaskItemStatus.Done })),
            ("CancelTask", (caller, _, project, task) => caller.PutAsync(ApiRoutes.Task(project, task), new UpdateTaskItemRequest { Title = "Hijacked", Status = TaskItemStatus.Cancelled })),
            ("TakeOverTask", (caller, context, project, task) => caller.PutAsync(ApiRoutes.Task(project, task), new UpdateTaskItemRequest { Title = "Hijacked", AssignedUserId = context.Attacker.Session.UserId })),
            ("MoveTaskUnderOwnParent", (caller, context, project, task) => caller.PutAsync(ApiRoutes.Task(project, task), new UpdateTaskItemRequest { Title = "Hijacked", ParentTaskId = context.Attacker.ParentTask.Id })),
            ("StartTask", (caller, _, project, task) => caller.PostAsync(ApiRoutes.StartTask(project, task))),
            ("CompleteTask", (caller, _, project, task) => caller.PostAsync(ApiRoutes.CompleteTask(project, task))),
            ("DeleteTask", (caller, _, project, task) => caller.DeleteAsync(ApiRoutes.Task(project, task))),
            ("ListAttachments", (caller, _, project, task) => caller.GetAsync(ApiRoutes.Attachments(project, task))),
            ("UploadAttachment", (caller, _, project, task) => caller.UploadAsync(ApiRoutes.Attachments(project, task), "planted.txt", Guid.NewGuid().ToByteArray())),
            ("ReadTaskComments", (caller, _, project, task) => caller.GetAsync(ApiRoutes.TaskComments(project, task))),
            ("AddTaskComment", (caller, _, project, task) => caller.PostAsync(ApiRoutes.TaskComments(project, task), new SaveCommentRequest { Content = "Planted" }))
        };

        foreach (var operation in operations)
        {
            foreach (var project in Projects)
            {
                foreach (var task in Tasks)
                {
                    if (project.Label == AttackerSource && task.Label == AttackerSource)
                    {
                        continue;
                    }

                    attacks.Add(new CrossUserAttack(
                        $"{operation.Name}(project: {project.Label}, task: {task.Label})",
                        project.Label == AttackerSource ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                        context => operation.Execute(context.Attacker.Session, context, project.Resolve(context), task.Resolve(context))));
                }
            }
        }
    }

    // Operations addressed by a project, a task and an attachment: every mix of the three.
    private static void AddAttachmentAttacks(List<CrossUserAttack> attacks)
    {
        var operations = new List<(string Name, Func<ApiSession, Guid, Guid, Guid, Task<HttpResponseMessage>> Execute)>
        {
            ("DownloadAttachment", (caller, project, task, attachment) => caller.GetAsync(ApiRoutes.AttachmentContent(project, task, attachment))),
            ("DeleteAttachment", (caller, project, task, attachment) => caller.DeleteAsync(ApiRoutes.Attachment(project, task, attachment)))
        };

        foreach (var operation in operations)
        {
            foreach (var project in Projects)
            {
                foreach (var task in AttachmentTasks)
                {
                    foreach (var attachment in Attachments)
                    {
                        if (project.Label == AttackerSource && task.Label == AttackerSource && attachment.Label == AttackerSource)
                        {
                            continue;
                        }

                        attacks.Add(new CrossUserAttack(
                            $"{operation.Name}(project: {project.Label}, task: {task.Label}, attachment: {attachment.Label})",
                            project.Label == AttackerSource ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                            context => operation.Execute(context.Attacker.Session, project.Resolve(context), task.Resolve(context), attachment.Resolve(context))));
                    }
                }
            }
        }
    }

    // Changing and deleting a comment, addressed by a project and the comment: every mix of the two.
    private static void AddCommentAttacks(List<CrossUserAttack> attacks)
    {
        var operations = new List<(string Name, Func<ApiSession, Guid, Guid, Task<HttpResponseMessage>> Execute)>
        {
            ("UpdateComment", (caller, project, comment) => caller.PutAsync(ApiRoutes.Comment(project, comment), new SaveCommentRequest { Content = "Hijacked" })),
            ("DeleteComment", (caller, project, comment) => caller.DeleteAsync(ApiRoutes.Comment(project, comment)))
        };

        foreach (var operation in operations)
        {
            foreach (var project in Projects)
            {
                foreach (var comment in Comments)
                {
                    if (project.Label == AttackerSource && comment.Label == AttackerSource)
                    {
                        continue;
                    }

                    attacks.Add(new CrossUserAttack(
                        $"{operation.Name}(project: {project.Label}, comment: {comment.Label})",
                        project.Label == AttackerSource ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                        context => operation.Execute(context.Attacker.Session, project.Resolve(context), comment.Resolve(context))));
                }
            }
        }
    }

    // Files are also found by their hash together with a task. The task decides: a foreign one is forbidden,
    // a missing one is not found, and through an own task a foreign or unknown hash finds nothing.
    private static void AddHashAttacks(List<CrossUserAttack> attacks)
    {
        foreach (var hash in Hashes)
        {
            foreach (var task in AttachmentTasks)
            {
                if (hash.Label == AttackerSource && task.Label == AttackerSource)
                {
                    continue;
                }

                var ownTask = task.Label == AttackerSource;
                var refusal = task.Label == MissingSource ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;

                attacks.Add(new CrossUserAttack(
                    $"FindAttachmentByHash(hash: {hash.Label}, task: {task.Label})",
                    ownTask ? HttpStatusCode.OK : refusal,
                    context => context.Attacker.Session.GetAsync(ApiRoutes.AttachmentsBySha256(hash.Resolve(context), task.Resolve(context))),
                    ownTask ? EmptyList : null));

                attacks.Add(new CrossUserAttack(
                    $"DownloadAttachmentByHash(hash: {hash.Label}, task: {task.Label})",
                    ownTask ? HttpStatusCode.NotFound : refusal,
                    context => context.Attacker.Session.GetAsync(ApiRoutes.AttachmentContentBySha256(hash.Resolve(context), task.Resolve(context)))));
            }
        }
    }

    // Requests on the attacker's own project that name something foreign in the body: a foreign task as the
    // parent, or a user outside the project as the assignee, which would put a task on that user's list.
    private static void AddPlantingAttacks(List<CrossUserAttack> attacks)
    {
        foreach (var parent in ForeignParents)
        {
            attacks.Add(new CrossUserAttack(
                $"CreateOwnTask(parent: {parent.Label})",
                HttpStatusCode.NotFound,
                context => context.Attacker.Session.PostAsync(ApiRoutes.Tasks(context.Attacker.Project.Id), new CreateTaskItemRequest { Title = "Planted", ParentTaskId = parent.Resolve(context) })));

            attacks.Add(new CrossUserAttack(
                $"MoveOwnTask(parent: {parent.Label})",
                HttpStatusCode.NotFound,
                context => context.Attacker.Session.PutAsync(ApiRoutes.Task(context.Attacker.Project.Id, context.Attacker.OwnTask.Id), new UpdateTaskItemRequest { Title = context.Attacker.OwnTask.Title, AssignedUserId = context.Attacker.Session.UserId, ParentTaskId = parent.Resolve(context) })));
        }

        foreach (var user in ForeignUsers)
        {
            attacks.Add(new CrossUserAttack(
                $"CreateOwnTask(assignee: {user.Label})",
                HttpStatusCode.BadRequest,
                context => context.Attacker.Session.PostAsync(ApiRoutes.Tasks(context.Attacker.Project.Id), new CreateTaskItemRequest { Title = "Planted", AssignedUserId = user.Resolve(context) })));

            attacks.Add(new CrossUserAttack(
                $"CreateOwnSubTask(assignee: {user.Label})",
                HttpStatusCode.BadRequest,
                context => context.Attacker.Session.PostAsync(ApiRoutes.SubTasks(context.Attacker.Project.Id, context.Attacker.ParentTask.Id), new CreateTaskItemRequest { Title = "Planted", AssignedUserId = user.Resolve(context) })));

            attacks.Add(new CrossUserAttack(
                $"ReassignOwnTask(assignee: {user.Label})",
                HttpStatusCode.BadRequest,
                context => context.Attacker.Session.PutAsync(ApiRoutes.Task(context.Attacker.Project.Id, context.Attacker.OwnTask.Id), new UpdateTaskItemRequest { Title = context.Attacker.OwnTask.Title, AssignedUserId = user.Resolve(context) })));
        }
    }
}
