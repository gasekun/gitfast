# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

`gitFast` is a Windows desktop GUI for browsing Git repositories, built with **Avalonia 12** (net10.0) and **ReactiveUI**. The app launches a single `MainWindow` that lists local/remote branches, tags, and commits; clicking a commit shows the file list and per-line diff for that commit. All Git data is fetched by shelling out to the `git` CLI from `Services/GitService.cs`.

The repository to inspect is chosen at launch — the first CLI argument (if any) is taken as the path, otherwise the current working directory is used (see `Program.cs:Main`). The user can also change the repo at runtime via the folder picker (`MainWindow.axaml.cs:ChangeRepo_OnPointerPressed`), which trims the trailing `\` from the picked path before calling `GitService.SetMainPath`.

## Build / run

This is a single-project .NET solution. From the repo root:

```
dotnet build gitFast.sln
dotnet run --project gitFast/gitFast.csproj -- "C:\path\to\repo"
```

The first positional argument is the path to the Git repository to open. The build output is a `WinExe` (`<OutputType>WinExe</OutputType>` in `gitFast.csproj`).

There are **no tests** in the repo — `gitFast` is the only project in the solution.

## Architecture

### MVVM layout
- `gitFast/ViewModels/ViewModelBase.cs` — base class inheriting `ReactiveObject` (from ReactiveUI).
- `gitFast/ViewModels/MainWindowViewModel.cs` — the only view-model. Holds `ObservableCollection<BranchInfo>` for local/remote branches and tags, `ObservableCollection<CommitInfo>` for the commit list, plus the currently selected commit and file. All git calls go through `GitService` and are `await`ed.
- `gitFast/Views/MainWindow.axaml` / `MainWindow.axaml.cs` — the single window. The code-behind is thin glue: it forwards pointer-press events to the view-model and draws the commit graph onto a `Canvas` named `GitGraph` via `GenerateGraphAsync`.

### View ↔ ViewModel wiring
- `App.axaml.cs` instantiates `MainWindow` and assigns `new MainWindowViewModel()` as its `DataContext`.
- `ViewLocator.cs` is a default Avalonia data-template that maps `*ViewModel` → `*View` by name; not currently used since the only view is wired directly in `App.axaml.cs`.
- `gitFast/Ext/ListBoxExtensions.cs` adds an `ScrollToItem` attached property to `ListBox` that calls `ScrollIntoView` when the bound value changes. The `Commits` `ListBox` uses it via `ext:ListBoxExtensions.ScrollToItem="{Binding SelectedCommit}"` so the list scrolls to keep the selection visible.

### Services layer — `Services/GitService.cs`
A `static` class. It owns the `MainPath` (set once at startup or via the folder picker) and every public method shells out to `git` via `Process.Start`. `RunGit` returns `string[]` (lines), or `[]` on any non-zero exit code — callers therefore can't distinguish "no output" from "git failed." All file-system git work happens in the `MainPath` working directory. Notable methods:
- `GetCommits` — `git log --topo-order --pretty=...` parses a tab-separated format; also extracts branch / remote-branch / tag refs from the `%D` column and stores them as `Tag` records on the commit.
- `GetFilesCommit` — `git show ... --name-status`, with an octal-decoding pass for paths containing quoted non-ASCII bytes (`OctalToDecimal`).
- `GetFileDiff` / `GetFileEditCount` — `git show ... --numstat` and plain `git show`, used to populate `FileCommit.Lines` and `FileCommit.CountAdd/CountRemove`.
- `SelectBranch` — for `origin/*` refs, runs `git switch -c` to create a local tracking branch; otherwise plain `git switch`.
- `GitFetch` — `fetch --all` then `pull --all`.

### Models (`gitFast/Models/`)
- `CommitInfo` — `ReactiveRecord` (so all properties can use the `field` keyword). `Offset` is `int * 24` and is used as the left padding of each commit row to make room for the graph lanes; the graph code in `MainWindow.axaml.cs` mutates it.
- `FileCommit` — `ReactiveRecord`; loads its diff lines and `+/-` counts on demand via `LoadLines`/`LoadCount`, called from the view-model after selection. `ParseLines` walks the unified diff and tags each line `Add` / `Remove` / `Hunk` / `Default`; hunk `@@` lines reset the displayed `LinePos`.
- `FileLineCommit` — a `record struct` carrying the line text, position, and `LineType`.
- `BranchInfo`, `StatusFile`, `LineType`, `Tag`/`TagType` — small immutable types / enums.

### Styling / resources
- `MainResources.axaml` — color palette (`slate-*`, named semantic aliases like `bg`, `panel`, `border`, `text-muted`, `text-faint`) plus the SVG `PathGeometry` icons (`Git`, `FolderOutline`, `Magnify`, `Autorenew`, `TagOutline`, `Web`, `Close`, …) used as `PathIcon` resources.
- `MainStyles.axaml` — control styles / `Classes.*` selectors (`CommitRow`, `FileRow`, `SideItem`, `Btn`, `RefBadge`, `StatusChip`, `Mono`, `SearchTextBox`, …). New visual treatments should be added there as `Classes` rather than inlining XAML.
- `App.axaml` registers `<SimpleTheme/>` (from `Avalonia.Themes.Simple`) and merges `MainStyles.axaml` + `MainResources.axaml`; theme is locked to `Dark` via `RequestedThemeVariant="Dark"`.

### Commit graph rendering
`MainWindow.axaml.cs:GenerateGraphAsync` is where the visual graph (colored nodes + bezier/line connectors) is constructed. It walks `model.Commits` top-to-bottom, tracking an in-flight `parents` list of pending parent edges; for each row it draws an `Ellipse` node on the `GitGraph` `Canvas` and a `Path` per branch edge, colored from the 8-entry `brushes` palette by lane index. A second pass (lines 169-173) smooths lane offsets for non-adjacent parents. **Note:** the method currently returns early after `await Task.Yield()` (line 97-99) — the graph generation is disabled in the checked-in code; the `Canvas` stays invisible (`GitGraph.IsVisible="{Binding DisplayGraph}"` in the XAML is the only path that surfaces it).

## Conventions / gotchas
- C# `field` keyword is used everywhere for `ReactiveRecord` / `ReactiveObject` properties (`AvaloniaUseCompiledBindingsByDefault` is on, so the XAML expects compiled bindings — keep `x:DataType` on every templated control).
- `LangVersion` is `14` — newer C# features (collection expressions, primary constructors, `field`) are available.
- `Avalonia.Controls.WebView` is referenced but unused (commented out in `MainWindow.axaml`).
- `GitService.RunGit` swallows stderr and returns `[]` on any non-zero exit — surface failures by extending `RunGit` rather than wrapping individual callers.
- The view-model is constructed in `App.axaml.cs` and the repository path is set in `Program.cs` before `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`; `MainWindow` calls `Control_OnLoaded` → `InitRepo` → `model.Init` on first render.
