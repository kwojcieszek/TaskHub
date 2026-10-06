using System.Net;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// The steps most tests start from, each performed through the API as the given caller.
/// </summary>
public static class ApiSessionExtensions
{
    public static Task<ProjectDto> CreateProjectAsync(this ApiSession session, string name = "Project", Guid? projectGroupId = null)
    {
        return session.PostAsync<ProjectDto>(ApiRoutes.Projects, new CreateProjectRequest { Name = name, ProjectGroupId = projectGroupId }, HttpStatusCode.Created);
    }

    public static Task<ProjectGroupDto> CreateProjectGroupAsync(this ApiSession session, string name = "Group")
    {
        return session.PostAsync<ProjectGroupDto>(ApiRoutes.ProjectGroups, new CreateProjectGroupRequest { Name = name }, HttpStatusCode.Created);
    }

    public static Task<ProjectMemberDto> AddMemberAsync(this ApiSession session, Guid projectId, Guid userId, ProjectMemberRole role)
    {
        return session.PostAsync<ProjectMemberDto>(ApiRoutes.ProjectMembers(projectId), new AddProjectMemberRequest { UserId = userId, Role = role });
    }

    public static Task<TaskItemDto> CreateTaskAsync(this ApiSession session, Guid projectId, string title = "Task", DateTime? dueDate = null)
    {
        return session.PostAsync<TaskItemDto>(ApiRoutes.Tasks(projectId), new CreateTaskItemRequest { Title = title, DueDate = dueDate });
    }

    public static Task<TaskItemDto> CreateSubTaskAsync(this ApiSession session, Guid projectId, Guid parentTaskId, string title = "Subtask")
    {
        return session.PostAsync<TaskItemDto>(ApiRoutes.SubTasks(projectId, parentTaskId), new CreateTaskItemRequest { Title = title });
    }

    public static async Task<AttachmentDto> UploadAttachmentAsync(this ApiSession session, Guid projectId, Guid taskId, string fileName, byte[] content, string contentType = "application/octet-stream")
    {
        var response = await session.UploadAsync(ApiRoutes.Attachments(projectId, taskId), fileName, content, contentType);

        return await response.ReadAsync<AttachmentDto>();
    }
}
