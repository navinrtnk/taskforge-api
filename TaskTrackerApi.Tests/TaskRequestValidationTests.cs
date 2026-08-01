using System.ComponentModel.DataAnnotations;
using TaskTrackerApi.Dtos;
using Xunit;

namespace TaskTrackerApi.Tests;

public sealed class TaskRequestValidationTests
{
    [Fact]
    public void CreateRequest_WhenTitleIsMissing_ReturnsUsefulError()
    {
        var errors = Validate(new CreateTaskRequest { Title = string.Empty });

        Assert.Contains(errors, error => error.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void CreateRequest_WhenTitleIsTooLong_ReturnsUsefulError()
    {
        var errors = Validate(new CreateTaskRequest { Title = new string('a', 201) });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Title must be between 1 and 200 characters.");
    }

    [Fact]
    public void UpdateRequest_WhenDescriptionIsTooLong_ReturnsUsefulError()
    {
        var errors = Validate(new UpdateTaskRequest
        {
            Title = "Task",
            Description = new string('a', 2001)
        });

        Assert.Contains(errors, error =>
            error.ErrorMessage == "Description cannot exceed 2000 characters.");
    }

    private static IReadOnlyList<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}
