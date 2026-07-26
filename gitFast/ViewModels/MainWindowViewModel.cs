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
    } = false;

    public string NameRepo
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string StatusMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Ready";

    public bool IsBusy
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<WorkingFile> WorkingFiles
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public List<WorkingFile> UnstagedFiles => WorkingFiles.Where(item => item is { IsStaged: false }).ToList();
    public List<WorkingFile> StagedFiles => WorkingFiles.Where(item => item is { IsStaged: true }).ToList();

    public bool IsCommitSidebarOpen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CommitMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public int StagedCount => WorkingFiles.Count(f => f is { IsStaged: true, IsStagedForCommit: true });

    public bool IsWorkingTreeEmpty => WorkingFiles.Count == 0;

    public void SetStatus(string message, bool busy = true)
    {
        StatusMessage = message;
        IsBusy = busy;
    }

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
        
        await LoadWorkingFiles();

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
        {
            SetStatus($"Loading files for {commit.IdShort}…");
            commit.Files = await GitService.GetFilesCommit(commit.Id);
        }

        if (commit.Files.Count > 0)
            await Task.Run( async () =>
            {
                await SelectFileCommit(commit.Files[0]);

                foreach (var commitFile in commit.Files) await commitFile.LoadCount(commit.Id);
            });

        SetStatus($"Selected {commit.IdShort}", busy: false);
    }

    public async Task SelectFileCommit(FileCommit fileCommit)
    {
        foreach (var file in SelectedCommit!.Files) file.IsSelected = false;

        fileCommit.IsSelected = true;

        SelectedFileCommit = fileCommit;

        SetStatus($"Loading diff for {fileCommit.Path}…");
        await fileCommit.LoadLines(SelectedCommit.Id);
        SetStatus($"Selected {fileCommit.Path}", busy: false);
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
    
    public async Task LoadWorkingFiles()
    {
        SetStatus("Loading working tree…");
        var files = await GitService.GetStatus();
        foreach (var file in files)
        {
            if (file.IsStaged) file.IsStagedForCommit = true;
        }
        WorkingFiles = new ObservableCollection<WorkingFile>(files);
        this.RaisePropertyChanged(nameof(StagedCount));
        this.RaisePropertyChanged(nameof(IsWorkingTreeEmpty));
        this.RaisePropertyChanged(nameof(UnstagedFiles));
        this.RaisePropertyChanged(nameof(StagedFiles));
        SetStatus($"Loaded {files.Count} working file(s)", busy: false);
    }

    public void ToggleSidebar()
    {
        IsCommitSidebarOpen = !IsCommitSidebarOpen;
        
    }
    
    public async Task OnFileStagedForCommitChanged(WorkingFile file)
    {
        if (file is { IsStagedForCommit: true, IsStaged: false })
        {
            SetStatus($"Staging {file.Path}…");
            await GitService.StageFiles([file.Path]);
            file.IsStaged = true;
            SetStatus($"Staged {file.Path}", busy: false);
        }
        
        if (file is { IsStagedForCommit: false, IsStaged: true })
        {
            SetStatus($"Unstaging {file.Path}…");
            await GitService.UnstageFiles([file.Path]);
            file.IsStaged = false;
            SetStatus($"Unstaged {file.Path}", busy: false);
        }
        
        this.RaisePropertyChanged(nameof(UnstagedFiles));
        this.RaisePropertyChanged(nameof(StagedFiles));
        this.RaisePropertyChanged(nameof(StagedCount));
    }
    
    public async Task StageAll()
    {
        SetStatus("Staging all files…");
        await GitService.StageAll();
        foreach (var file in WorkingFiles)
        {
            file.IsStaged = true;
            file.IsStagedForCommit = true;
        }
        this.RaisePropertyChanged(nameof(StagedCount));
        SetStatus($"Staged {WorkingFiles.Count} file(s)", busy: false);
    }

    public void SetAllStagedForCommit(bool value)
    {
        foreach (var file in WorkingFiles) file.IsStagedForCommit = value;
        this.RaisePropertyChanged(nameof(StagedCount));
    }

    public async Task RefreshWorkingFiles()
    {
        await LoadWorkingFiles();
    }
    
    public async Task<bool> EnsureSomethingStagedForCommitAsync(Func<Task<bool>> confirmStageAll)
    {
        if (StagedCount > 0) return true;

        if (!await confirmStageAll()) return false;

        await StageAll();
        return true;
    }

    public async Task CommitSelected()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            SetStatus("Commit message cannot be empty", busy: false);
            return;
        }

        SetStatus("Creating commit…");
        await GitService.Commit(CommitMessage);

        CommitMessage = "";
        this.RaisePropertyChanged(nameof(CommitMessage));
        await LoadWorkingFiles();
        SetStatus("Commit created", busy: false);
    }
}