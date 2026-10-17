namespace MyStreamTimer.Core.Automation;

public enum CommandStatus { Applied, Ignored, Rejected }

public readonly record struct CommandResult(CommandStatus Status, string? Message = null)
{
    public bool Applied => Status == CommandStatus.Applied;
    public static CommandResult Success => new(CommandStatus.Applied);
}
