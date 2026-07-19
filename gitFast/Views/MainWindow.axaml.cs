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
using gitFast.Models;
using gitFast.Services;
using gitFast.ViewModels;
using FileCommit = gitFast.Models.FileCommit;

namespace gitFast.Views;

public partial class MainWindow : Window
{
    private const double offsetY = 52;
    private const double offsetX = 24;
    private readonly List<SolidColorBrush> Brushes;

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

        Brushes =
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
            await model.Init();
            LoaderIcon.IsVisible = false;
            await GenerateGraphAsync(model);
        }
    }

    private async Task GenerateGraphAsync(MainWindowViewModel model)
    {
        await Task.Yield();

        var sizePoint = new Point(12, 12);
        var parents = new List<GitGraph>();
        var linesColors = new Dictionary<int, SolidColorBrush>();
        var arrayCommitOffset = new int[model.Commits.Count];
        var currentOffset = 1;

        GitGraph.IsVisible = false;

        GitGraph.Children.Clear();

        for (var i = 0; i < model.Commits.Count; i++)
        {
            var modelCommit = model.Commits[i];

            var first = parents.FindIndex(item => item.Id == modelCommit.Id);

            if (first != -1)
            {
                currentOffset = first + 1;
                arrayCommitOffset[i] = currentOffset;

                var currentPoint2 = new Point(arrayCommitOffset[i] * offsetX, 21 + i * offsetY);

                var paths = parents.Where(item => item.Id == modelCommit.Id).Select(item => item.Path);

                foreach (var path in paths) AddConnection(path, currentPoint2, sizePoint);

                parents.RemoveAll(item => item.Id == modelCommit.Id);
            }

            if (i == 0)
                arrayCommitOffset[i] = currentOffset;

            var currentPoint = new Point(arrayCommitOffset[i] * offsetX, 21 + i * offsetY);
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

                var point = new Point((j + 1) * offsetX, 21 + i * offsetY);

                parents[j].Path.Segments!.Add(new LineSegment
                {
                    Point = point + sizePoint / 2
                });
            }
        }

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
                Point2 = new Point(line2.Point.X, currentPoint2.Y + sizePoint.Y / 2) + new Point(-offsetX, 0)
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


        var brush = Brushes[(int)(path.StartPoint.X / offsetX) % Brushes.Count];

        var geometry = new PathGeometry();
        geometry.Figures!.Add(path);

        GitGraph.Children.Add(new Path
        {
            Stroke = brush,
            StrokeThickness = 2,
            Data = geometry,
            Opacity = 0.7
        });
    }

    private void AddCommitNode(CommitInfo commit, Point point, Point pointSize)
    {
        //(int)(point.X / pointSize.X)
        var brush = Brushes[(int)(point.X / offsetX) % Brushes.Count];

        if (commit.Message.Contains("Merge"))
        {
            var mergeNode = new Ellipse
            {
                Width = pointSize.X + 6,
                Height = pointSize.Y + 6,
                Stroke = brush,
                StrokeThickness = 1,
                Opacity = 0.6,
                ZIndex = 2
            };
            Canvas.SetLeft(mergeNode, point.X - 3);
            Canvas.SetTop(mergeNode, point.Y - 3);
            GitGraph.Children.Add(mergeNode);
        }

        var node = new Ellipse
        {
            Width = pointSize.X,
            Height = pointSize.Y,
            Fill = brush,
            ZIndex = 2
        };
        Canvas.SetLeft(node, point.X);
        Canvas.SetTop(node, point.Y);
        GitGraph.Children.Add(node);
    }

    private async void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model && sender is TextBox box) await model.SearchCommits(box.Text!);
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model) await GenerateGraphAsync(model);
    }

    private async void FetchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await GitService.GitFetch();

        if (DataContext is MainWindowViewModel model)
        {
            LoaderIcon.IsVisible = true;
            await model.Init();
            LoaderIcon.IsVisible = false;
            await GenerateGraphAsync(model);
        }
    }

    private async void Branch_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: BranchInfo branch } && DataContext is MainWindowViewModel model)
        {
            LoaderIcon.IsVisible = true;
            model.SelectBranch(branch.Name);
            await model.Init();
            LoaderIcon.IsVisible = false;
            await GenerateGraphAsync(model);
        }
    }
}

internal sealed record GitGraph(string Id, PathFigure Path);