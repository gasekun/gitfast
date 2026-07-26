using ReactiveUI;

namespace gitFast.Models;

public sealed record WorkingFile : ReactiveRecord
{
    public string Path { get; }
    public string? OldPath { get; }
    public string StatusCode { get; }

    // Whether git has this file in the index. Updated when we stage/unstage.
    public bool IsStaged
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsUntracked { get; }

    // Set by stage/unstage actions, not by the checkbox itself.
    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // Whether the user has ticked this file in the commit sidebar.
    // When true AND IsStaged, the file will be part of the next commit.
    public bool IsStagedForCommit
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CountAdd
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CountRemove
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string TextStatus
    {
        get
        {
            if (IsUntracked) return "?";
            if (StatusCode.StartsWith("A", System.StringComparison.Ordinal)) return "A";
            if (StatusCode.StartsWith("D", System.StringComparison.Ordinal)) return "D";
            if (StatusCode.StartsWith("R", System.StringComparison.Ordinal)) return "R";
            return "M";
        }
    }

    public bool IsCreated => TextStatus == "A" || TextStatus == "?";
    public bool IsModified => TextStatus == "M" || TextStatus == "R";
    public bool IsRemoved => TextStatus == "D";

    public WorkingFile(
        string path,
        string statusCode,
        bool isStaged,
        bool isUntracked,
        string? oldPath = null)
    {
        Path = path;
        StatusCode = statusCode;
        IsStaged = isStaged;
        IsUntracked = isUntracked;
        OldPath = oldPath;
    }
}
