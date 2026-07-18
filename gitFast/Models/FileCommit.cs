using System.Collections.Generic;
using System.Text.RegularExpressions;
using gitFast.Services;
using ReactiveUI;

namespace gitFast.Models;

public partial record FileCommit(string Path, StatusFile Status)
    : ReactiveRecord
{
    public bool IsCreated => Status == StatusFile.Add;
    public bool IsModified => Status == StatusFile.Modify;
    public bool IsRemoved => Status == StatusFile.Delete;

    public List<FileLineCommit> Lines
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];
    public string TextStatus => Status switch
    {
        StatusFile.Add => "A",
        StatusFile.Modify => "M",
        StatusFile.Delete => "D",
        _ => "E"
    };

    public bool IsSelected
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

    public void LoadCount(string commitId)
    {
        var countFile = GitService.GetFileEditCount(commitId, Path);

        CountAdd = countFile.add;
        CountRemove = countFile.remove;
    }

    public void LoadLines(string commitId)
    {
        var diff = GitService.GetFileDiff(commitId, Path);

        Lines = ParseLines(diff);
    }

    private static List<FileLineCommit> ParseLines(string[] diff)
    {
        var list = new List<FileLineCommit>();

        var regexPlus = RegexPlus();
        var regexMinus = RegexMinus();
        var currentLinePos = 0;

        foreach (var line in diff)
        {
            var fileLine = new FileLineCommit(line, 0);

            if (line.StartsWith("+"))
            {
                fileLine.Type = LineType.Add;
            }
            if (line.StartsWith("-"))
            {
                fileLine.Type = LineType.Remove;
            }

            fileLine.LinePos = currentLinePos++;

            if (line.StartsWith("@"))
            {
                fileLine.Type = LineType.Hunk;
                var linePos = regexMinus.Match(line).Groups;

                currentLinePos = int.Parse(linePos[1].Value);
            }

            list.Add(fileLine);
        }
        return list;
    }

    [GeneratedRegex(@"\+(\d+)")]
    private static partial Regex RegexPlus();
    [GeneratedRegex(@"\-(\d+)")]
    private static partial Regex RegexMinus();
}