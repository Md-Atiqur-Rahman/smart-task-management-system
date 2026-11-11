namespace Datavanced.SmartTaskManagement.Application.Abstractions.Email;

/// <summary>
/// One plain-text email to one recipient. Contains personal data (address and name): never log it
/// (log the notification id instead).
/// </summary>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string Body);
