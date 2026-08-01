using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TaskTrackerApi.Controllers;
using TaskTrackerApi.Data;
using TaskTrackerApi.Dtos;
using TaskTrackerApi.Models;
using Xunit;

namespace TaskTrackerApi.Tests;

public sealed class TasksControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsTasksNewestFirst()
    {
        await using var db = CreateContext();
        db.Tasks.AddRange(
            NewTask("Older", createdAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewTask("Newer", createdAtUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetAll(null, CancellationToken.None);

        var tasks = AssertOk<IReadOnlyList<TaskItem>>(result.Result);
        Assert.Equal(["Newer", "Older"], tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task GetAll_WhenCompletionFilterProvided_ReturnsOnlyMatchingTasks()
    {
        await using var db = CreateContext();
        db.Tasks.AddRange(NewTask("Open"), NewTask("Done", isCompleted: true));
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetAll(true, CancellationToken.None);

        var task = Assert.Single(AssertOk<IReadOnlyList<TaskItem>>(result.Result));
        Assert.Equal("Done", task.Title);
    }

    [Fact]
    public async Task GetById_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        await using var db = CreateContext();

        var result = await CreateController(db).GetById(123, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_TrimsTextAndPersistsTask()
    {
        await using var db = CreateContext();
        var request = new CreateTaskRequest
        {
            Title = "  Write tests  ",
            Description = "  Cover the controller  "
        };

        var result = await CreateController(db).Create(request, CancellationToken.None);

        var response = Assert.IsType<CreatedAtActionResult>(result.Result);
        var task = Assert.IsType<TaskItem>(response.Value);
        Assert.Equal(nameof(TasksController.GetById), response.ActionName);
        Assert.Equal("Write tests", task.Title);
        Assert.Equal("Cover the controller", task.Description);
        Assert.False(task.IsCompleted);
        Assert.NotEqual(default, task.CreatedAtUtc);
        Assert.Equal(task.Id, (await db.Tasks.SingleAsync()).Id);
    }

    [Fact]
    public async Task Create_NormalizesWhitespaceDescriptionToNull()
    {
        await using var db = CreateContext();
        var request = new CreateTaskRequest { Title = "Task", Description = "   " };

        var result = await CreateController(db).Create(request, CancellationToken.None);

        var response = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Null(Assert.IsType<TaskItem>(response.Value).Description);
    }

    [Fact]
    public async Task Create_EmitsStructuredInformationLog()
    {
        await using var db = CreateContext();
        var logger = new RecordingLogger();

        await new TasksController(db, logger).Create(
            new CreateTaskRequest { Title = "Logged task" }, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Created task", entry.Message);
    }

    [Fact]
    public async Task Update_WhenTaskExists_ChangesAndPersistsAllEditableFields()
    {
        await using var db = CreateContext();
        var existing = NewTask("Old title");
        db.Tasks.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateController(db).Update(existing.Id, new UpdateTaskRequest
        {
            Title = "  New title ",
            Description = "  New description ",
            IsCompleted = true
        }, CancellationToken.None);

        var task = AssertOk<TaskItem>(result.Result);
        Assert.Equal("New title", task.Title);
        Assert.Equal("New description", task.Description);
        Assert.True(task.IsCompleted);
        Assert.NotNull(task.UpdatedAtUtc);
        Assert.Equal("New title", (await db.Tasks.SingleAsync()).Title);
    }

    [Fact]
    public async Task SetCompletion_WhenTaskExists_UpdatesCompletionAndTimestamp()
    {
        await using var db = CreateContext();
        var existing = NewTask("Task");
        db.Tasks.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateController(db)
            .SetCompletion(existing.Id, true, CancellationToken.None);

        var task = AssertOk<TaskItem>(result.Result);
        Assert.True(task.IsCompleted);
        Assert.NotNull(task.UpdatedAtUtc);
    }

    [Fact]
    public async Task Delete_WhenTaskExists_RemovesTaskAndReturnsNoContent()
    {
        await using var db = CreateContext();
        var existing = NewTask("Task");
        db.Tasks.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateController(db).Delete(existing.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.Tasks.ToListAsync());
    }

    [Fact]
    public async Task Mutations_WhenTaskDoesNotExist_ReturnNotFound()
    {
        await using var db = CreateContext();
        var controller = CreateController(db);

        var update = await controller.Update(123, new UpdateTaskRequest { Title = "Missing" }, CancellationToken.None);
        var completion = await controller.SetCompletion(123, true, CancellationToken.None);
        var delete = await controller.Delete(123, CancellationToken.None);

        Assert.IsType<NotFoundResult>(update.Result);
        Assert.IsType<NotFoundResult>(completion.Result);
        Assert.IsType<NotFoundResult>(delete);
    }

    [Fact]
    public async Task GetStats_ReturnsTotalCompletedAndOpenCounts()
    {
        await using var db = CreateContext();
        db.Tasks.AddRange(NewTask("Open"), NewTask("Done one", true), NewTask("Done two", true));
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetStats(CancellationToken.None);

        var stats = AssertOk<TaskStatsResponse>(result.Result);
        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.Completed);
        Assert.Equal(1, stats.Open);
    }

    private static TaskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TaskDbContext(options);
    }

    private static TasksController CreateController(TaskDbContext db) =>
        new(db, NullLogger<TasksController>.Instance);

    private static TaskItem NewTask(
        string title,
        bool isCompleted = false,
        DateTime? createdAtUtc = null) => new()
        {
            Title = title,
            IsCompleted = isCompleted,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };

    private static T AssertOk<T>(ActionResult? result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private sealed class RecordingLogger : ILogger<TasksController>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
