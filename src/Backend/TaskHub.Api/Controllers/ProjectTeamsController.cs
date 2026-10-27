using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/projects/{projectId:guid}/teams")]
public class ProjectTeamsController : ApiControllerBase
{
    private readonly IProjectTeamService _projectTeamService;

    public ProjectTeamsController(IProjectTeamService projectTeamService)
    {
        _projectTeamService = projectTeamService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTeamDto>>> GetAll(Guid projectId, CancellationToken cancellationToken)
    {
        var teams = await _projectTeamService.GetAsync(projectId, cancellationToken);
        return Ok(teams);
    }

    // The teams that can still be added to the project.
    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTeamDto>>> GetAvailable(Guid projectId, CancellationToken cancellationToken)
    {
        var teams = await _projectTeamService.GetAvailableAsync(projectId, cancellationToken);
        return Ok(teams);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectTeamDto>> Add(Guid projectId, AddProjectTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await _projectTeamService.AddAsync(projectId, request, cancellationToken);
        return Ok(team);
    }

    [HttpDelete("{userGroupId:guid}")]
    public async Task<IActionResult> Remove(Guid projectId, Guid userGroupId, CancellationToken cancellationToken)
    {
        await _projectTeamService.RemoveAsync(projectId, userGroupId, cancellationToken);
        return NoContent();
    }
}
