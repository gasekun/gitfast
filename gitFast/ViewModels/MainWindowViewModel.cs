using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using gitFast.Models;
using gitFast.Services;
using ReactiveUI;

namespace gitFast.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private List<CommitInfo> commits = [];

    public ObservableCollection<BranchInfo> LocalBranches
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<BranchInfo> RemoteBranches
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<BranchInfo> Tags
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<CommitInfo> Commits
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public CommitInfo? SelectedCommit
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FileCommit? SelectedFileCommit
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsFetching
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CurrentBranch
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public bool DisplayGraph
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public string NameRepo
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public void ClearCommits() => Commits.Clear();

    public async Task Init()
    {
        SelectedCommit = null;
        SelectedFileCommit = null;

        NameRepo = Path.GetFileName(GitService.MainPath);

        LocalBranches = new ObservableCollection<BranchInfo>(await GitService.GetBranches());
        RemoteBranches = new ObservableCollection<BranchInfo>(await GitService.GetBranches(false));
        commits = await GitService.GetCommits();
        if (LocalBranches.Count > 0)
            CurrentBranch = LocalBranches.FirstOrDefault(item => item.IsCurrentBranch).Name ?? "";

        Tags = new ObservableCollection<BranchInfo>(commits.SelectMany(item => item.Tags).Where(item => item.IsTag)
            .Select(item => new BranchInfo(item.Name, false)));

        Commits = new ObservableCollection<CommitInfo>(commits);

        if (Commits.Count > 0)
            await SelectCommit(Commits[0]);
    }

    public async Task SelectCommit(CommitInfo commit)
    {
        foreach (var commitInfo in Commits) commitInfo.IsSelected = false;

        commit.IsSelected = true;

        SelectedCommit = commit;

        SelectedFileCommit = null;

        if (commit.Files.Count == 0)
            commit.Files = await GitService.GetFilesCommit(commit.Id);

        if (commit.Files.Count > 0)
            await Task.Run(() =>
            {
                SelectFileCommit(commit.Files[0]);

                foreach (var commitFile in commit.Files) commitFile.LoadCount(commit.Id);
            });
    }

    public async Task SelectFileCommit(FileCommit fileCommit)
    {
        foreach (var file in SelectedCommit!.Files) file.IsSelected = false;

        fileCommit.IsSelected = true;

        SelectedFileCommit = fileCommit;

        await fileCommit.LoadLines(SelectedCommit.Id);
    }

    public Task SearchCommits(string boxText)
    {
        try
        {
            var searchText = boxText.Trim().ToLower();

            Commits = new ObservableCollection<CommitInfo>(commits
                .Where(item => item.Message.ToLower().Contains(searchText)).ToList());

            DisplayGraph = Commits.Count == commits.Count;
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public async Task SelectBranch(string branch)
    {
        CurrentBranch = branch;
        await GitService.SelectBranch(branch);
    }

    public async Task SelectCommitByTag(BranchInfo branch)
    {
        var commit = commits.FirstOrDefault(item => item.Tags.Any(tags => tags.Name.Contains(branch.Name)));
        if (commit != null)
        {
            await SelectCommit(commit);
        }
    }

    public async Task RemoveBranch(BranchInfo branch)
    {
        await GitService.RemoveBranch(branch.Name);
        LocalBranches = new ObservableCollection<BranchInfo>(await GitService.GetBranches());
        if (LocalBranches.Count > 0)
            await SelectBranch(LocalBranches[0].Name);
    }
}