using Claims.Api.Auth;
using Claims.Application.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Produces("application/json")]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    /// <summary>Open claims per assignee plus the unassigned queue. ClaimStaff get only their own row.</summary>
    [HttpGet("workload", Name = "GetWorkloadReport")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<WorkloadReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Workload(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWorkloadQuery(), cancellationToken));

    /// <summary>Outstanding reserve on open claims by market, type, status and currency.</summary>
    [HttpGet("exposure", Name = "GetExposureReport")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<ExposureReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Exposure(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetExposureQuery(), cancellationToken));
}
