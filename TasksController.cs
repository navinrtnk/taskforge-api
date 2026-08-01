using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTrackerApi.Data;
using TaskTrackerApi.Dtos;
using TaskTrackerApi.Models;

namespace TaskTrackerApi.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(TaskDbContext dbContext, ILogger<TasksController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItem>>> GetAll(
        [FromQuery] bool? completed, CancellationToken cancellationToken)
    {
        var query = dbContext.Tasks.AsNoTracking();
        if (completed.HasValue)
        {
            query = query.Where(task => task.IsCompleted == completed.Value);
        }

        return Ok(await query.OrderByDescending(task => task.CreatedAtUtc)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<TaskStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var total = await dbContext.Tasks.CountAsync(cancellationToken);
        var completed = await dbContext.Tasks.CountAsync(task => task.IsCompleted, cancellationToken);
        return Ok(new TaskStatsResponse(total, completed, total - completed));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == id, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create(
        CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created task {TaskId}", task.Id);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskItem>> Update(
        int id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.FindAsync([id], cancellationToken);
        if (task is null) return NotFound();

        task.Title = request.Title.Trim();
        task.Description = Normalize(request.Description);
        task.IsCompleted = request.IsCompleted;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated task {TaskId}", task.Id);
        return Ok(task);
    }

    [HttpPatch("{id:int}/complete")]
    public async Task<ActionResult<TaskItem>> SetCompletion(
        int id, [FromQuery] bool completed = true, CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks.FindAsync([id], cancellationToken);
        if (task is null) return NotFound();

        task.IsCompleted = completed;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Set task {TaskId} completion to {IsCompleted}", task.Id, completed);
        return Ok(task);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.FindAsync([id], cancellationToken);
        if (task is null) return NotFound();

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted task {TaskId}", task.Id);
        return NoContent();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
