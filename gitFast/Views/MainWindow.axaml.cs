using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using gitFast.Models;
using gitFast.ViewModels;
using FileCommit = gitFast.Models.FileCommit;
using System.Threading.Tasks;

namespace gitFast.Views;

public partial class MainWindow : Window
{
    private List<SolidColorBrush> Brushes;
    private int currentOffsetBrush = 0;
    public MainWindow()
    {
        Application.Current!.TryGetResource("blue-400", out var brush1);
        Application.Current!.TryGetResource("emerald-400", out var brush2);
        Application.Current!.TryGetResource("red-400", out var brush3);
        Application.Current!.TryGetResource("amber-400", out var brush4);
        Application.Current!.TryGetResource("violet-400", out var brush5);
        Application.Current!.TryGetResource("indigo-400", out var brush6);
        Application.Current!.TryGetResource("purple-400", out var brush7);
        Application.Current!.TryGetResource("lime-400", out var brush8);
        Application.Current!.TryGetResource("green-400", out var brush9);
        Application.Current!.TryGetResource("sky-400", out var brush10);
        Application.Current!.TryGetResource("cyan-400", out var brush11);

        Brushes =
        [
            (brush2 as SolidColorBrush)!,
            (brush7 as SolidColorBrush)!,
            (brush1 as SolidColorBrush)!,
            (brush5 as SolidColorBrush)!,
            (brush10 as SolidColorBrush)!,
            (brush9 as SolidColorBrush)!,
            (brush3 as SolidColorBrush)!,
            (brush4 as SolidColorBrush)!,
            (brush6 as SolidColorBrush)!,
            (brush8 as SolidColorBrush)!,
            (brush11 as SolidColorBrush)!
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

    private Task GenerateGraphAsync(MainWindowViewModel model)
    {
        const double offsetY = 52;
        const double offsetX = 24;
        var sizePoint = new Point(12, 12);
        var parents = new List<GitGraph>();
        var arrayOffsetCommits = new int[model.Commits.Count];
        var currentPoint = new Point(offsetX, 21);
        var currentOffset = 1;

        GitGraph.Children.Clear();

        for (var i = 0; i < model.Commits.Count; i++)
        {
            var modelCommit = model.Commits[i];
            currentOffsetBrush++;

            var first = parents.FindIndex(item => item.Id == modelCommit.Id);

            if (first != -1)
            {
                currentOffset = first + 1;

                currentPoint = new Point(offsetX * currentOffset, currentPoint.Y);

                var allPoints = parents
                    .Where(item => item.Id == modelCommit.Id)
                    .SelectMany(item => item.Points)
                    .ToList();

                foreach (var point in allPoints)
                    AddConnection(point, currentPoint, sizePoint);

                parents.RemoveAll(item => item.Id == modelCommit.Id);
            }

            arrayOffsetCommits[i] = currentOffset;
            
            AddCommitNode(modelCommit, currentPoint, sizePoint);

            if (modelCommit.ParentId.Length > 0)
            {
                for (var index = 0; index < modelCommit.ParentId.Length; index++)
                {
                    var parentId = modelCommit.ParentId[index];

                    if (index == 0)
                        parents.Insert(currentOffset - 1, new GitGraph(parentId, [currentPoint]));
                    else
                        parents.Add(new GitGraph(parentId, [currentPoint]));
                }
            }

            currentPoint += new Point(0, offsetY);
        }

        return Task.CompletedTask;
    }

    private void AddConnection(Point start, Point end, Point pointSize)
    {
        //(int)(start.X / pointSize.X)
        var brush = Brushes[currentOffsetBrush % Brushes.Count];
        var centerOffset = new Point(pointSize.X / 2, pointSize.Y / 2);
        var size = new Size(pointSize.X, pointSize.Y);
        var startCenter = start + centerOffset;
        var endCenter = end + centerOffset;

        if (Math.Abs(start.X - end.X) <= 0.01)
        {
            GitGraph.Children.Add(new Line
            {
                StartPoint = startCenter,
                EndPoint = endCenter,
                Stroke = brush,
                StrokeThickness = 2
            });
            return;
        }

        var pathFigure = new PathFigure { StartPoint = startCenter, IsClosed = false };
        if (start.X > end.X)
        {
            pathFigure.Segments!.Add(new LineSegment { Point = new Point(start.X, end.Y) + new Point(pointSize.X / 2, 0) });
            pathFigure.Segments!.Add(new ArcSegment
            {
                Point = new Point(start.X, end.Y) + centerOffset - new Point(pointSize.X / 2, 0),
                Size = size
            });
        }
        else
        {
            pathFigure.Segments!.Add(new LineSegment { Point = new Point(end.X, start.Y) + new Point(0, pointSize.Y / 2) });
            pathFigure.Segments!.Add(new ArcSegment
            {
                Point = new Point(end.X, start.Y) + centerOffset + new Point(0, pointSize.Y / 2),
                Size = size
            });
        }

        pathFigure.Segments!.Add(new LineSegment { Point = endCenter });
        
        var geometry = new PathGeometry();
        geometry.Figures!.Add(pathFigure);
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
        var brush = Brushes[currentOffsetBrush % Brushes.Count];
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
        if (DataContext is MainWindowViewModel model && sender is TextBox box)
        {
            await model.SearchCommits(box.Text!);
        }
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model)
        {
            await GenerateGraphAsync(model);
        }
    }
}

internal sealed record GitGraph(string Id, List<Point> Points);
