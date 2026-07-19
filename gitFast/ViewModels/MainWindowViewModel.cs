using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using gitFast.Models;
using gitFast.Services;
using ReactiveUI;

namespace gitFast.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private List<CommitInfo> commits = [];

    public ObservableCollection<BranchInfo> Branches
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

    public async Task Init()
    {
        await Task.Run(() =>
        {
            Branches = new ObservableCollection<BranchInfo>(GitService.GetBranches());
            commits = GitService.GetCommits();
            Commits = new ObservableCollection<CommitInfo>(commits);

            if (Commits.Count > 0)
                SelectCommit(Commits[0]);
        });
    }

    public async void SelectCommit(CommitInfo commit)
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

    public void SelectFileCommit(FileCommit fileCommit)
    {
        foreach (var file in SelectedCommit!.Files) file.IsSelected = false;

        fileCommit.IsSelected = true;

        SelectedFileCommit = fileCommit;

        fileCommit.LoadLines(SelectedCommit.Id);
    }

    public Task SearchCommits(string boxText)
    {
        try
        {
            var searchText = boxText.Trim().ToLower();

            Commits = new ObservableCollection<CommitInfo>(commits
                .Where(item => item.Message.ToLower().Contains(searchText)).ToList());
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public void SelectBranch(string branch)
    {
        GitService.SelectBranch(branch);
    }
}