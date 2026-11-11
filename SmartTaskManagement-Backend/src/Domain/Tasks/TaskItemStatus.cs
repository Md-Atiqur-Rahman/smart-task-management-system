namespace Datavanced.SmartTaskManagement.Domain.Tasks;

/// <summary>Workflow status of a task. Any status may change to any other (no transition table).</summary>
public enum TaskItemStatus
{
    ToDo = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}
