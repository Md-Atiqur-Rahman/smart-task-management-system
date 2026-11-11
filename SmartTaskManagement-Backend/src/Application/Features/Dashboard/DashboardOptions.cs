using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard;

/// <summary>Dashboard settings, bound from the "Dashboard" configuration section and validated on start.</summary>
public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>How many days ahead (inclusive of today) a task counts as upcoming.</summary>
    [Range(1, 90)]
    public int UpcomingDays { get; init; } = 7;

    /// <summary>Maximum number of upcoming tasks returned.</summary>
    [Range(1, 50)]
    public int UpcomingLimit { get; init; } = 10;
}
