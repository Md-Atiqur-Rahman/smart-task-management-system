using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Notifications;

public sealed class NotificationMessagesTests
{
    [Theory]
    [InlineData(NotificationType.TaskAssigned, "Fix login", "Alice", "Alice assigned you the task \"Fix login\".")]
    [InlineData(NotificationType.TaskUnassigned, "Fix login", "Alice", "Alice removed the task \"Fix login\" from you.")]
    [InlineData(NotificationType.ProjectOwnerAssigned, "Apollo", "Bob", "Bob made you the owner of the project \"Apollo\".")]
    [InlineData(NotificationType.ProjectOwnerRemoved, "Apollo", "Bob", "Bob removed your ownership of the project \"Apollo\".")]
    public void Render_ReturnsExactMessage(NotificationType type, string subject, string actorName, string expected)
    {
        NotificationMessages.Render(type, subject, actorName).ShouldBe(expected);
    }

    [Fact]
    public void Render_UnknownType_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NotificationMessages.Render((NotificationType)99, "S", "A"));
    }
}
