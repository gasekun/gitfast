# gitFast

### A desktop Git client built with C#, Avalonia, and ReactiveUI

Explore repository history, inspect changes, and create commits in a custom dark interface. gitFast brings branches, commit history, file diffs, and working-tree changes into one desktop workspace.

This pet project demonstrates reactive desktop development, Git CLI integration, and custom UI rendering with .NET.

## Features

- **Repository navigation** — open a local repository at startup or choose another folder from the application.
- **Branches and tags** — browse local and remote branches, switch branches, and jump to tagged commits in the loaded history.
- **Commit search** — filter the current branch's history by commit message.
- **Commit graph** — generate a color-coded graph of parent relationships and merges using the Graph button.
- **Diff inspection** — view changed files, addition/deletion counts, and unified diffs with highlighted additions, removals, and hunk headers.
- **Working-tree workflow** — review staged and unstaged files, stage or unstage changes, and write commit messages in the changes sidebar.

## Technical Highlights

| Area | Implementation |
| --- | --- |
| Desktop UI | Avalonia 12, XAML, shared styles, and a custom dark palette |
| Application state | MVVM with ReactiveUI, observable collections, and reactive properties |
| Git integration | Asynchronous Git processes through a centralized service using `ProcessStartInfo.ArgumentList` |
| Graph rendering | Canvas-based nodes and connecting paths derived from commit parent IDs |
| Diff handling | Unified-diff parsing into line models, with file details loaded on selection |
| Language and runtime | C# 14 and .NET 10, with nullable reference types and compiled Avalonia bindings |

## Getting Started

### Prerequisites

- Windows, the project's current desktop target.
- .NET 10 SDK.
- Git installed and available on `PATH`.
- A local Git repository to explore.

### Build and run

From the root of this source checkout:

```powershell
dotnet restore gitFast.sln
dotnet build gitFast.sln
dotnet run --project gitFast/gitFast.csproj -- "C:\path\to\repository"
```

The repository argument is optional. Without it, gitFast uses the current working directory. To explore this checkout itself:

```powershell
dotnet run --project gitFast/gitFast.csproj -- .
```

### Try the workflow

1. Open a repository and select a commit to inspect its changed files.
2. Select a file to read its diff, or search for a commit by message.
3. Click **Graph** to visualize the loaded history.
4. Open **+ Commit** to review working-tree changes, stage files, and create a commit.

The **Fetch** button currently runs `git fetch --all` followed by `git pull --all`, so it can update the current branch as well as remote references.

### Publish

```powershell
dotnet publish gitFast/gitFast.csproj -c Release
```

The publish output is written under `gitFast/bin/Release/net10.0/publish/`. Git must also be installed on the machine running the application.

## Project Structure

```text
gitFast.sln
gitFast/
|-- Models/              Commits, branches, working files, and diff lines
|-- Services/            Git CLI execution and output parsing
|-- ViewModels/          Reactive state and repository workflows
|-- Views/               Main window, interactions, and graph rendering
|-- Ext/                 Attached control behaviors
|-- Assets/              Packaged application assets
|-- MainResources.axaml  Colors, icons, and control themes
|-- MainStyles.axaml     Shared UI styles
`-- Program.cs           Application entry point and repository selection
```

## Development Status

gitFast is a pet project under development. The repository currently has no automated test suite; validation relies on building the solution and manually exercising repository navigation, diffs, and working-tree operations. See [AGENTS.md](AGENTS.md) for contributor guidelines.
