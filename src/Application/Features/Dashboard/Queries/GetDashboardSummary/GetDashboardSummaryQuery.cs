using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetDashboardSummary;

/// <summary>The caller's dashboard numbers.</summary>
public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;
