using System;
using System.Collections.Generic;
using ReactiveUI;

namespace gitFast.Models;

public sealed record CommitInfo(
    string Id,
    string Message,
    string Author,
    DateTime Date,
    string[] ParentId,
    bool IsHead = false,
    bool IsSelected = false) : ReactiveRecord
{
    public List<FileCommit> Files
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public List<Tag> Tags
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = IsSelected;

    public int Offset
    {
        get => 24 * field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1;

    public string IdShort => Id[..8];
}

public record Tag(TagType Type, string Name)
{
    public bool IsBranch => Type == TagType.Branch;
    public bool IsTag => Type == TagType.Tag;
    public bool IsRemoteBranch => Type == TagType.RemoteBranch;
};

public enum TagType
{
    Branch,
    Tag,
    RemoteBranch
}