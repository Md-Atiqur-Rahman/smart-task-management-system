namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

public sealed record WhoAmIResponse(bool IsAuthenticated, Guid? UserId, IReadOnlyCollection<string> Roles);
