using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ChangeTaskStatus;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.UpdateTask;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Tasks;

public sealed class TaskCommandValidatorTests
{
    private readonly CreateTaskCommandValidator _create = new();
    private readonly UpdateTaskCommandValidator _update = new();

    private static CreateTaskCommand Create(
        string title = "T",
        string? description = null,
        TaskPriority? priority = null,
        Guid? assigneeId = null) =>
        new(Guid.NewGuid(), title, description, priority, null, assigneeId);

    [Fact]
    public void CreateValidator_WhitespaceTitle_Fails()
    {
        _create.Validate(Create(title: "   ")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_TwoHundredCharTitleWithSpaces_Passes()
    {
        var title = "  " + new string('a', TaskItem.TitleMaxLength) + "  ";

        _create.Validate(Create(title: title)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateValidator_TitleOverMax_Fails()
    {
        _create.Validate(Create(title: new string('a', TaskItem.TitleMaxLength + 1))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_DescriptionOverMax_Fails()
    {
        var description = new string('d', TaskItem.DescriptionMaxLength + 1);

        _create.Validate(Create(description: description)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_UndefinedPriority_Fails()
    {
        _create.Validate(Create(priority: (TaskPriority)99)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_EmptyGuidAssignee_Fails()
    {
        _create.Validate(Create(assigneeId: Guid.Empty)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_EmptyProjectId_Fails()
    {
        _create.Validate(new CreateTaskCommand(Guid.Empty, "T", null, null, null, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void UpdateValidator_WhitespaceTitle_Fails()
    {
        _update.Validate(new UpdateTaskCommand(Guid.NewGuid(), "  ", null, TaskPriority.Low, null))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void UpdateValidator_UndefinedPriority_Fails()
    {
        _update.Validate(new UpdateTaskCommand(Guid.NewGuid(), "T", null, (TaskPriority)42, null))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ChangeStatusValidator_UndefinedStatus_Fails()
    {
        new ChangeTaskStatusCommandValidator()
            .Validate(new ChangeTaskStatusCommand(Guid.NewGuid(), (TaskItemStatus)7)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AssignValidator_EmptyGuidAssignee_Fails()
    {
        new AssignTaskCommandValidator()
            .Validate(new AssignTaskCommand(Guid.NewGuid(), Guid.Empty)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AssignValidator_NullAssignee_Passes()
    {
        new AssignTaskCommandValidator()
            .Validate(new AssignTaskCommand(Guid.NewGuid(), null)).IsValid.ShouldBeTrue();
    }
}
