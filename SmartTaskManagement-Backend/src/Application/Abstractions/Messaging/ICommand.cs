using System.Diagnostics.CodeAnalysis;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

/// <summary>A state-changing request without a return value. Handled by <see cref="ICommandHandler{TCommand}"/>.</summary>
[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Marker interface used as a generic constraint: identifies commands at compile time so a query can never reach a command handler.")]
public interface ICommand;

/// <summary>A state-changing request returning <typeparamref name="TResponse"/>.</summary>
[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Marker interface used as a generic constraint: identifies commands at compile time so a query can never reach a command handler.")]
public interface ICommand<TResponse>;
