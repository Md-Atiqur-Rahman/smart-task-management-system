// SAMPLE: copy-source, delete or rename
namespace Datavanced.SmartTaskManagement.Api.Controllers.Samples;

/// <summary>
/// HTTP body for POST /api/samples. Fields are nullable on purpose: a missing
/// value reaches FluentValidation (400 with an errors map) instead of failing
/// during JSON deserialization.
/// </summary>
public sealed record CreateSampleRequest(string? Name, string? Description);
