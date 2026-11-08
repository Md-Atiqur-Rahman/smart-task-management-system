namespace Contoso.TemplateApp.IntegrationTests.Api.TestSupport;

public sealed record WhoAmIResponse(bool IsAuthenticated, Guid? UserId, IReadOnlyCollection<string> Roles);
