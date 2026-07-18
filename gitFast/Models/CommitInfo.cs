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

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = IsSelected;
}