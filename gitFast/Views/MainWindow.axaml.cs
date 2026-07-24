using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using gitFast.Models;
using gitFast.Services;
using gitFast.ViewModels;
using FileCommit = gitFast.Models.FileCommit;

namespace gitFast.Views;

public partial class MainWindow : Window
{
    private const double OffsetY = 52;
    private const double OffsetX = 24;
    private readonly List<SolidColorBrush> brushes;

    private List<Control> childrens = [];

    public MainWindow()
    {
        Application.Current!.TryGetResource("red-400", out var red);
        Application.Current!.TryGetResource("amber-400", out var amber);
        Application.Current!.TryGetResource("lime-400", out var lime);
        Application.Current!.TryGetResource("emerald-400", out var emerald);
        Application.Current!.TryGetResource("cyan-400", out var cyan);
        Application.Current!.TryGetResource("blue-400", out var blue);
        Application.Current!.TryGetResource("violet-400", out var violet);
        Application.Current!.TryGetResource("fuchsia-400", out var fuchsia);

        brushes =
        [
            (red as SolidColorBrush)!,
            (blue as SolidColorBrush)!,
            (amber as SolidColorBrush)!,
            (fuchsia as SolidColorBrush)!,
            (emerald as SolidColorBrush)!,
            (cyan as SolidColorBrush)!,
            (violet as SolidColorBrush)!,
            (lime as SolidColorBrush)!
        ];

        InitializeComponent();
    }

    private async Task InitRepo(MainWindowViewModel model, Func<Task>? func = null)
    {
        CommitsScrollViewer.IsVisible = false;
        LoaderIcon.IsVisible = true;

        if (func != null)
        {
            await func();
        }

        await model.Init();
        await GenerateGraphAsync(model);
        LoaderIcon.IsVisible = false;
        CommitsScrollViewer.IsVisible = true;
    }

    private void CommitRow_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (DataContext is MainWindowViewModel model && sender is Border { DataContext: CommitInfo commitInfo })
            model.SelectCommit(commitInfo);
    }

    private void FileRow_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (DataContext is MainWindowViewModel model && sender is Border { DataContext: FileCommit fileCommit })
            model.SelectFileCommit(fileCommit);
    }

    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model)
        {
            await InitRepo(model);
        }
    }

    private async Task GenerateGraphAsync(MainWindowViewModel model)
    {
        await Task.Yield();
        
        var sizePoint = new Point(12, 12);
        var parents = new List<GitGraph>();
        var arrayCommitOffset = new int[model.Commits.Count];
        var currentOffset = 1;

        childrens.Clear();

        for (var i = 0; i < model.Commits.Count; i++)
        {
            var modelCommit = model.Commits[i];

            var first = parents.FindIndex(item => item.Id == modelCommit.Id);

            if (first != -1)
            {
                currentOffset = first + 1;
                arrayCommitOffset[i] = currentOffset;

                var currentPoint2 = new Point(arrayCommitOffset[i] * OffsetX, 21 + i * OffsetY);

                var paths = parents.Where(item => item.Id == modelCommit.Id).Select(item => item.Path);

                foreach (var path in paths) AddConnection(path, currentPoint2, sizePoint);

                parents.RemoveAll(item => item.Id == modelCommit.Id);
            }

            if (i == 0)
                arrayCommitOffset[i] = currentOffset;

            var currentPoint = new Point(arrayCommitOffset[i] * OffsetX, 21 + i * OffsetY);
            AddCommitNode(modelCommit, currentPoint, sizePoint);

            if (modelCommit.ParentId.Length > 0)
                for (var index = 0; index < modelCommit.ParentId.Length; index++)
                {
                    var parentId = modelCommit.ParentId[index];

                    if (index == 0)
                        parents.Insert(currentOffset - 1,
                            new GitGraph(parentId,
                                new PathFigure
                                {
                                    StartPoint = currentPoint + sizePoint / 2, IsClosed = false
                                }));
                    else
                        parents.Add(new GitGraph(parentId,
                            new PathFigure
                            {
                                StartPoint = currentPoint + sizePoint / 2, IsClosed = false
                            }));
                }

            for (var j = 0; j < parents.Count; j++)
            {
                if (currentOffset == j + 1) continue;

                var point = new Point((j + 1) * OffsetX, 21 + i * OffsetY);

                parents[j].Path.Segments!.Add(new LineSegment
                {
                    Point = point + sizePoint / 2
                });
            }

            modelCommit.Offset = parents.Count == 1 ? 1 : parents.Count;
        }

        for (var i = 1; i < model.Commits.Count - 1; i++)
        {
            if (Math.Abs(model.Commits[i - 1].Offset / 24 - model.Commits[i + 1].Offset / 24) > 2)
                model.Commits[i].Offset = (model.Commits[i - 1].Offset / 24 + model.Commits[i + 1].Offset / 24) / 2 + 1;
        }

        GitGraph.IsHitTestVisible = false;
        GitGraph.IsVisible = false;
        GitGraph.Children.Clear();
        GitGraph.BeginInit();

        GitGraph.Children.AddRange(childrens);

        GitGraph.EndInit();
        GitGraph.IsVisible = true;
    }

    private void AddConnection(PathFigure path, Point currentPoint2, Point sizePoint)
    {
        if (path.Segments!.Count > 0 && path.Segments!.Last() is LineSegment line &&
            Math.Abs(line.Point.X - currentPoint2.X - sizePoint.X / 2) < 0.001f)
        {
            path.Segments.Remove(line);

            path.Segments!.Add(new QuadraticBezierSegment
            {
                Point1 = line.Point,
                Point2 = currentPoint2 + sizePoint / 2
            });
        }
        else if (path.Segments!.Count > 0 && path.Segments!.Last() is LineSegment line2 &&
                 Math.Abs(line2.Point.X - currentPoint2.X - sizePoint.X / 2) > 0.001f)
        {
            path.Segments!.Add(new QuadraticBezierSegment
            {
                Point1 = new Point(line2.Point.X, currentPoint2.Y + sizePoint.Y / 2),
                Point2 = new Point(line2.Point.X, currentPoint2.Y + sizePoint.Y / 2) + new Point(-OffsetX, 0)
            });

            path.Segments.Add(new LineSegment
            {
                Point = currentPoint2 + sizePoint / 2
            });
        }
        else
        {
            if (Math.Abs(path.StartPoint.X - currentPoint2.X - sizePoint.X / 2) < 0.001f)
                path.Segments!.Add(new LineSegment
                {
                    Point = currentPoint2 + sizePoint / 2
                });
            else
                path.Segments!.Add(new QuadraticBezierSegment
                {
                    Point1 = new Point(path.StartPoint.X, currentPoint2.Y) + sizePoint / 2,
                    Point2 = currentPoint2 + sizePoint / 2
                });
        }

        if (path.Segments.Count > 2)
            if (path.Segments[0] is LineSegment line1 && path.Segments[1] is LineSegment line2)
            {
                path.Segments.Remove(line1);
                path.Segments.Remove(line2);

                path.Segments.Insert(0, new QuadraticBezierSegment
                {
                    Point1 = line1.Point,
                    Point2 = line2.Point
                });
            }


        var brush = brushes[(int)(path.StartPoint.X / OffsetX) % brushes.Count];

        var geometry = new PathGeometry();
        geometry.Figures!.Add(path);

        childrens.Add(new Path
        {
            Stroke = brush,
            StrokeThickness = 2,
            Data = geometry,
            Opacity = 0.7,
        });
    }

    private void AddCommitNode(CommitInfo commit, Point point, Point pointSize)
    {
        //(int)(point.X / pointSize.X)
        var brush = brushes[(int)(point.X / OffsetX) % brushes.Count];

        if (commit.Message.Contains("Merge"))
        {
            var mergeNode = new Ellipse
            {
                Width = pointSize.X + 6,
                Height = pointSize.Y + 6,
                Stroke = brush,
                StrokeThickness = 1,
                Opacity = 0.6,
                ZIndex = 2,
            };
            Canvas.SetLeft(mergeNode, point.X - 3);
            Canvas.SetTop(mergeNode, point.Y - 3);
            childrens.Add(mergeNode);
        }

        var node = new Ellipse
        {
            Width = pointSize.X,
            Height = pointSize.Y,
            Fill = brush,
            ZIndex = 2,
        };
        Canvas.SetLeft(node, point.X);
        Canvas.SetTop(node, point.Y);
        childrens.Add(node);
    }

    private async void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model && sender is TextBox box) await model.SearchCommits(box.Text!);
    }

    private async void FetchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model)
        {
            model.IsFetching = true;
            await InitRepo(model, async () => { await GitService.GitFetch(); });
            model.IsFetching = false;
        }
    }

    private async void Branch_Pressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(sender as Control);

        if (properties.Properties.IsLeftButtonPressed
            && properties.Position != new Point(0, 0)
            && sender is Border { DataContext: BranchInfo branch }
            && DataContext is MainWindowViewModel model)
        {
            await InitRepo(model, async () => { model.SelectBranch(branch.Name); });
        }
    }

    private void TagsSideItem_Pressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(sender as Control).Properties;
        if (properties.IsLeftButtonPressed
            && sender is Border { DataContext: BranchInfo branch }
            && DataContext is MainWindowViewModel model)
        {
            model.SelectCommitByTag(branch);
        }
    }

    private void RemoveBranch_Pressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(sender as Control);

        if (properties.Properties.IsLeftButtonPressed
            && sender is Border { DataContext: BranchInfo branch }
            && DataContext is MainWindowViewModel model)
        {
            model.RemoveBranch(branch);
        }
    }

    private async void ChangeRepo_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Get TopLevel context from the current control or Window
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // Open the folder picker dialog
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a Project Folder",
            AllowMultiple = false
        });

        // Process the selection
        if (folders.Count > 0)
        {
            // Get the absolute path if available (primarily desktop platforms)
            var folderPath = folders[0].Path.LocalPath;
            GitService.SetMainPath(folderPath[..^1]);
            if (DataContext is MainWindowViewModel model)
                await InitRepo(model);
        }
    }
}

internal sealed record GitGraph(string Id, PathFigure Path);