using System.Net;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Domain.Enums;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Every operation on a project and what it contains, with the access it requires.
/// The permission matrix runs each of them as each kind of caller.
/// </summary>
public static class AccessOperations
{
    public static readonly IReadOnlyList<AccessOperation> All = new List<AccessOperation>
    {
        new("GetProject", AccessArea.Project, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.Project(scenario.Project.Id))),

        new("GetMembers", AccessArea.Project, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.ProjectMembers(scenario.Project.Id))),

        new("UpdateProject", AccessArea.Project, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PutAsync(ApiRoutes.Project(scenario.Project.Id), new UpdateProjectRequest { Name = "Renamed", IsActive = true })),

        new("DeactivateProject", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.OK,
            (caller, scenario) => caller.PutAsync(ApiRoutes.Project(scenario.Project.Id), new UpdateProjectRequest { Name = scenario.Project.Name, IsActive = false })),

        new("SetProjectGroup", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.OK,
            (caller, scenario) => caller.PutAsync(ApiRoutes.ProjectGroupOf(scenario.Project.Id), new SetProjectGroupRequest { ProjectGroupId = null })),

        new("AddMember", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new AddProjectMemberRequest { UserId = scenario.Cast.Stranger.UserId, Role = ProjectMemberRole.Editor })),

        new("RemoveMember", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.NoContent,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.ProjectMember(scenario.Project.Id, scenario.Cast.Viewer.UserId))),

        new("DeleteProject", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.NoContent,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.Project(scenario.Project.Id))),

        new("ListTasks", AccessArea.Task, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.Tasks(scenario.Project.Id))),

        new("GetTask", AccessArea.Task, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id))),

        new("GetSubTasks", AccessArea.Task, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.SubTasks(scenario.Project.Id, scenario.ParentTask.Id))),

        new("CreateTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.Tasks(scenario.Project.Id), new CreateTaskItemRequest { Title = "New task" })),

        new("CreateSubTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.SubTasks(scenario.Project.Id, scenario.ParentTask.Id), new CreateTaskItemRequest { Title = "New subtask" })),

        new("UpdateTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PutAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id), new UpdateTaskItemRequest { Title = "Renamed", Status = TaskItemStatus.ToDo, Priority = TaskItemPriority.High })),

        new("StartTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.StartTask(scenario.Project.Id, scenario.LeafTask.Id))),

        new("CompleteTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.CompleteTask(scenario.Project.Id, scenario.LeafTask.Id))),

        new("DeleteTask", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.NoContent,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id))),

        new("DeleteTaskWithSubTasks", AccessArea.Task, AccessLevel.Edit, HttpStatusCode.NoContent,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.Task(scenario.Project.Id, scenario.ParentTask.Id))),

        new("ListAttachments", AccessArea.Attachment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.Attachments(scenario.Project.Id, scenario.LeafTask.Id))),

        new("DownloadAttachment", AccessArea.Attachment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.AttachmentContent(scenario.Project.Id, scenario.LeafTask.Id, scenario.Attachment.Id))),

        new("FindAttachmentBySha256", AccessArea.Attachment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.AttachmentsBySha256(scenario.Attachment.Sha256, scenario.LeafTask.Id))),

        new("DownloadAttachmentBySha256", AccessArea.Attachment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.AttachmentContentBySha256(scenario.Attachment.Sha256, scenario.LeafTask.Id))),

        new("UploadAttachment", AccessArea.Attachment, AccessLevel.Edit, HttpStatusCode.OK,
            (caller, scenario) => caller.UploadAsync(ApiRoutes.Attachments(scenario.Project.Id, scenario.LeafTask.Id), "new.txt", Guid.NewGuid().ToByteArray())),

        new("DeleteAttachment", AccessArea.Attachment, AccessLevel.Edit, HttpStatusCode.NoContent,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.Attachment(scenario.Project.Id, scenario.LeafTask.Id, scenario.Attachment.Id))),

        new("ListTeams", AccessArea.Project, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.ProjectTeams(scenario.Project.Id))),

        new("ListAvailableTeams", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.AvailableProjectTeams(scenario.Project.Id))),

        new("AddTeam", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.OK,
            async (caller, scenario) =>
            {
                var team = await scenario.Cast.Admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Matrix team {Guid.NewGuid():N}" });

                return await caller.PostAsync(ApiRoutes.ProjectTeams(scenario.Project.Id), new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Editor });
            }),

        new("RemoveTeam", AccessArea.Project, AccessLevel.Manage, HttpStatusCode.NotFound,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.ProjectTeam(scenario.Project.Id, Guid.NewGuid()))),

        new("ListProjectComments", AccessArea.Comment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.ProjectComments(scenario.Project.Id))),

        new("AddProjectComment", AccessArea.Comment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.ProjectComments(scenario.Project.Id), new SaveCommentRequest { Content = "Note" })),

        new("ListTaskComments", AccessArea.Comment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.GetAsync(ApiRoutes.TaskComments(scenario.Project.Id, scenario.LeafTask.Id))),

        new("AddTaskComment", AccessArea.Comment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.TaskComments(scenario.Project.Id, scenario.LeafTask.Id), new SaveCommentRequest { Content = "Note" })),

        new("AddSubTaskComment", AccessArea.Comment, AccessLevel.View, HttpStatusCode.OK,
            (caller, scenario) => caller.PostAsync(ApiRoutes.TaskComments(scenario.Project.Id, scenario.SubTask.Id), new SaveCommentRequest { Content = "Note" })),

        // Nobody has written this comment, so whoever may look for it in the project is told it is not there.
        new("UpdateMissingComment", AccessArea.Comment, AccessLevel.View, HttpStatusCode.NotFound,
            (caller, scenario) => caller.PutAsync(ApiRoutes.Comment(scenario.Project.Id, Guid.NewGuid()), new SaveCommentRequest { Content = "Note" })),

        new("DeleteMissingComment", AccessArea.Comment, AccessLevel.View, HttpStatusCode.NotFound,
            (caller, scenario) => caller.DeleteAsync(ApiRoutes.Comment(scenario.Project.Id, Guid.NewGuid())))
    };

    public static AccessOperation Get(string name)
    {
        return All.Single(operation => operation.Name == name);
    }

    /// <summary>Every operation of the area paired with every kind of caller.</summary>
    public static TheoryData<string, Actor> Cases(AccessArea area)
    {
        var cases = new TheoryData<string, Actor>();

        foreach (var operation in All.Where(operation => operation.Area == area))
        {
            foreach (var actor in Enum.GetValues<Actor>())
            {
                cases.Add(operation.Name, actor);
            }
        }

        return cases;
    }
}
