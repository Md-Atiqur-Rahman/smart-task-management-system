namespace Datavanced.SmartTaskManagement.Api.RateLimiting;

/// <summary>
/// Marks an action that must also pass the stricter auth limit
/// (<c>RateLimiting:AuthPermitLimit</c> per <c>AuthWindowSeconds</c>) on top of the global limit.
/// Read by <see cref="SharedFixedWindowRateLimiter"/> from the endpoint metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class AuthRateLimitAttribute : Attribute;
