using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskTrackerApi.Data;
using TaskTrackerApi.Models;
using Xunit;

namespace TaskTrackerApi.Tests;

public sealed class SqliteIntegrationTests
{
    [Fact]
    public async Task Task_CanBePersistedAndReadWithUtcTimestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseSqlite(connection)
            .Options;
        var createdAtUtc = new DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc);

        await using (var writeContext = new TaskDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            writeContext.Tasks.Add(new TaskItem
            {
                Title = ".NET 10 integration test",
                CreatedAtUtc = createdAtUtc
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new TaskDbContext(options);
        var persistedTask = await readContext.Tasks.AsNoTracking().SingleAsync();

        Assert.Equal(".NET 10 integration test", persistedTask.Title);
        Assert.Equal(createdAtUtc, persistedTask.CreatedAtUtc);
    }
}
