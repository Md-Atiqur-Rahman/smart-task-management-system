using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Application.Features.Dashboard;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Dashboard;

public sealed class DashboardOptionsTests
{
    [Fact]
    public void Options_Defaults_PassValidation()
    {
        var options = new DashboardOptions();

        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true)
            .ShouldBeTrue();
        options.UpcomingDays.ShouldBe(7);
        options.UpcomingLimit.ShouldBe(10);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(91, 10)]
    [InlineData(7, 0)]
    [InlineData(7, 51)]
    public void Options_OutOfRange_FailValidation(int days, int limit)
    {
        var options = new DashboardOptions { UpcomingDays = days, UpcomingLimit = limit };

        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true)
            .ShouldBeFalse();
    }
}
