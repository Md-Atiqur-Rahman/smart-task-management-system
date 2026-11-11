using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Application.Features.Notifications;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Notifications;

public sealed class NotificationsOptionsTests
{
    private static bool IsValid(NotificationsOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

    [Fact]
    public void Options_Defaults_PassValidation()
    {
        var options = new NotificationsOptions();

        IsValid(options).ShouldBeTrue();
        options.RetentionDays.ShouldBe(7);
        options.ListLimit.ShouldBe(50);
        options.HeartbeatSeconds.ShouldBe(25);
        options.CleanupIntervalMinutes.ShouldBe(60);
        options.RedisChannel.ShouldBe("notifications");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void Options_RetentionOutOfRange_FailValidation(int days) =>
        IsValid(new NotificationsOptions { RetentionDays = days }).ShouldBeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void Options_ListLimitOutOfRange_FailValidation(int limit) =>
        IsValid(new NotificationsOptions { ListLimit = limit }).ShouldBeFalse();

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void Options_HeartbeatOutOfRange_FailValidation(int seconds) =>
        IsValid(new NotificationsOptions { HeartbeatSeconds = seconds }).ShouldBeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Options_CleanupIntervalOutOfRange_FailValidation(int minutes) =>
        IsValid(new NotificationsOptions { CleanupIntervalMinutes = minutes }).ShouldBeFalse();

    [Fact]
    public void Options_EmptyChannel_FailsValidation() =>
        IsValid(new NotificationsOptions { RedisChannel = string.Empty }).ShouldBeFalse();
}
