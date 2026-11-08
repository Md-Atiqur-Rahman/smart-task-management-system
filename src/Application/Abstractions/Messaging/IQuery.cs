using System.Diagnostics.CodeAnalysis;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

/// <summary>A read-only request returning <typeparamref name="TResponse"/>. Never changes state.</summary>
[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Marker interface used as a generic constraint: identifies queries at compile time so a command can never reach a query handler.")]
public interface IQuery<TResponse>;
