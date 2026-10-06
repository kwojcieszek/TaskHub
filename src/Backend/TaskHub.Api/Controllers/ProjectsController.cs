using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
public class ProjectsController : ApiControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectDto>>> GetAll([FromQuery] Guid? groupId, CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAccessibleProjectsAsync(groupId, cancellationToken);
        return Ok(projects);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(projectId, cancellationToken);
        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { projectId = project.Id, version = "1.0" }, project);
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.UpdateAsync(projectId, request, cancellationToken);
        return Ok(project);
    }

    [HttpPut("{projectId:guid}/group")]
    public async Task<ActionResult<ProjectDto>> SetGroup(Guid projectId, SetProjectGroupRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.SetGroupAsync(projectId, request, cancellationToken);
        return Ok(project);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(projectId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMemberDto>>> GetMembers(Guid projectId, CancellationToken cancellationToken)
    {
        var members = await _projectService.GetMembersAsync(projectId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await _projectService.AddMemberAsync(projectId, request, cancellationToken);
        return Ok(member);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        await _projectService.RemoveMemberAsync(projectId, userId, cancellationToken);
        return NoContent();
    }
}
