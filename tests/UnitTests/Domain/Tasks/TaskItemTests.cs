using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Tasks;

public sealed class TaskItemTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    public static TheoryData<TaskItemStatus, TaskItemStatus> AllStatusPairs()
    {
        var data = new TheoryData<TaskItemStatus, TaskItemStatus>();
        foreach (var from in Enum.GetValues<TaskItemStatus>())
        {
            foreach (var to in Enum.GetValues<TaskItemStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    private static TaskItem NewTask(Guid? assigneeId = null) =>
        TaskItem.Create(ProjectId, "Fix login", null, TaskPriority.Medium, null, assigneeId);

    [Fact]
    public void Create_ValidInput_TrimsAndStartsInToDo()
    {
        var task = TaskItem.Create(ProjectId, "  Fix login  ", "  details  ", TaskPriority.High, null, null);

        task.Title.ShouldBe("Fix login");
        task.Description.ShouldBe("details");
        task.Status.ShouldBe(TaskItemStatus.ToDo);
        task.Priority.ShouldBe(TaskPriority.High);
        task.ProjectId.ShouldBe(ProjectId);
        task.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionEmptyAfterTrim_IsNull(string? description) =>
        TaskItem.Create(ProjectId, "A", description, TaskPriority.Low, null, null).Description.ShouldBeNull();

    [Fact]
    public void Create_TitleWithSpacesAroundMaxLength_IsAccepted() =>
        TaskItem.Create(ProjectId, $"  {new string('t', TaskItem.TitleMaxLength)}  ", null, TaskPriority.Low, null, null)
            .Title.Length.ShouldBe(TaskItem.TitleMaxLength);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTitle_Throws(string title) =>
        Should.Throw<ArgumentException>(() => TaskItem.Create(ProjectId, title, null, TaskPriority.Low, null, null));

    [Fact]
    public void Create_TitleTooLong_Throws() =>
        Should.Throw<ArgumentException>(() =>
            TaskItem.Create(ProjectId, new string('t', TaskItem.TitleMaxLength + 1), null, TaskPriority.Low, null, null));

    [Fact]
    public void Create_DescriptionTooLong_Throws() =>
        Should.Throw<ArgumentException>(() =>
            TaskItem.Create(ProjectId, "A", new string('d', TaskItem.DescriptionMaxLength + 1), TaskPriority.Low, null, null));

    [Fact]
    public void Create_EmptyProjectId_Throws() =>
        Should.Throw<ArgumentException>(() =>
            TaskItem.Create(Guid.Empty, "A", null, TaskPriority.Low, null, null));

    [Fact]
    public void Create_EmptyAssigneeId_Throws() =>
        Should.Throw<ArgumentException>(() =>
            TaskItem.Create(ProjectId, "A", null, TaskPriority.Low, null, Guid.Empty));

    [Fact]
    public void Create_PastDueDate_IsAccepted()
    {
        var due = new DateOnly(2020, 1, 1);

        TaskItem.Create(ProjectId, "A", null, TaskPriority.Low, due, null).DueDate.ShouldBe(due);
    }

    [Theory]
    [MemberData(nameof(AllStatusPairs))]
    public void ChangeStatus_AnyToAny_ChangesWhenDifferent(TaskItemStatus from, TaskItemStatus to)
    {
        var task = NewTask();
        task.ChangeStatus(from);

        task.ChangeStatus(to).ShouldBe(from != to);

        task.Status.ShouldBe(to);
    }

    [Fact]
    public void Assign_SameAssignee_ReturnsFalse()
    {
        var assignee = Guid.NewGuid();
        var task = NewTask(assignee);

        task.Assign(assignee).ShouldBeFalse();

        task.AssigneeId.ShouldBe(assignee);
    }

    [Fact]
    public void Assign_Null_Unassigns()
    {
        var task = NewTask(Guid.NewGuid());

        task.Assign(null).ShouldBeTrue();

        task.AssigneeId.ShouldBeNull();
    }

    [Fact]
    public void Assign_EmptyGuid_Throws()
    {
        var task = NewTask();

        Should.Throw<ArgumentException>(() => task.Assign(Guid.Empty));
    }

    [Fact]
    public void UpdateDetails_NewValues_AreTrimmedAndStored()
    {
        var task = NewTask();
        var due = new DateOnly(2030, 5, 6);

        task.UpdateDetails(" New title ", "   ", TaskPriority.Critical, due);

        task.Title.ShouldBe("New title");
        task.Description.ShouldBeNull();
        task.Priority.ShouldBe(TaskPriority.Critical);
        task.DueDate.ShouldBe(due);
    }

    [Fact]
    public void Delete_SetsDeletedAt()
    {
        var task = NewTask();
        var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        task.Delete(at);

        task.IsDeleted.ShouldBeTrue();
        task.DeletedAt.ShouldBe(at);
    }
}
