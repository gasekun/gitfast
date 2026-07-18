namespace gitFast.Models;

public record struct FileLineCommit(string Line, int LinePos, LineType Type = LineType.Default)
{
    public bool IsAdd => Type == LineType.Add;
    public bool IsRemove => Type == LineType.Remove;
    public bool IsHunk => Type == LineType.Hunk;
    public bool IsDefault => Type == LineType.Default;
}