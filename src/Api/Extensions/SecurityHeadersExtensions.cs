namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class SecurityHeadersExtensions
{
    /// <summary>HSTS (outside Development), HTTPS redirection and baseline security headers.</summary>
    public static WebApplication UseHttpsAndSecurityHeaders(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.Use(static (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            return next(context);
        });
        return app;
    }
}
