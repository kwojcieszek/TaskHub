using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/project-groups")]
public class ProjectGroupsController : ApiControllerBase
{
    private readonly IProjectGroupService _projectGroupService;
    private readonly IProjectService _projectService;

    public ProjectGroupsController(IProjectGroupService projectGroupService, IProjectService projectService)
    {
        _projectGroupService = projectGroupService;
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _projectGroupService.GetAccessibleGroupsAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{projectGroupId:guid}")]
    public async Task<ActionResult<ProjectGroupDto>> GetById(Guid projectGroupId, CancellationToken cancellationToken)
    {
        var group = await _projectGroupService.GetByIdAsync(projectGroupId, cancellationToken);
        return Ok(group);
    }

    [HttpGet("{projectGroupId:guid}/projects")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectDto>>> GetProjects(Guid projectGroupId, CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAccessibleProjectsAsync(projectGroupId, cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectGroupDto>> Create(CreateProjectGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await _projectGroupService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { projectGroupId = group.Id, version = "1.0" }, group);
    }

    [HttpPut("{projectGroupId:guid}")]
    public async Task<ActionResult<ProjectGroupDto>> Update(Guid projectGroupId, UpdateProjectGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await _projectGroupService.UpdateAsync(projectGroupId, request, cancellationToken);
        return Ok(group);
    }

    [HttpDelete("{projectGroupId:guid}")]
    public async Task<IActionResult> Delete(Guid projectGroupId, CancellationToken cancellationToken)
    {
        await _projectGroupService.DeleteAsync(projectGroupId, cancellationToken);
        return NoContent();
    }
}
