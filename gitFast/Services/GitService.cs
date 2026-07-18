using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using gitFast.Models;
using FileCommit = gitFast.Models.FileCommit;

namespace gitFast.Services;

public static class GitService
{
    private static string mainPath = @"";

    public static void SetMainPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Repository path cannot be empty.", nameof(path));

        mainPath = path;
    }

    public static string[] GetFileDiff(string id, string file)
    {
        var output = RunGit("show", id, "--format=", "--", file);
        if (output.Length == 0)
            return [];

        var startIndex = FindFirstDiffLine(output);
        return startIndex >= output.Length ? [] : output[startIndex..];
    }

    public static (int add, int remove) GetFileEditCount(string id, string file)
    {
        var output = RunGit("show", id, "--numstat", "--format=", "--", file);
        if (output.Length == 0)
            return (0, 0);

        var parts = output[0].Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return (0, 0);

        return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var add) &&
               int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var remove)
            ? (add, remove)
            : (0, 0);
    }

    public static Task<List<FileCommit>> GetFilesCommit(string id)
    {
        var output = RunGit("show", id, "--format=", "--name-status");
        if (output.Length == 0)
            return Task.FromResult<List<FileCommit>>([]);

        var list = new List<FileCommit>(output.Length);
        foreach (var line in output)
        {
            if (!TryParseFileStatus(line, out var file, out var status))
                continue;

            list.Add(new FileCommit(file, status));
        }

        return Task.FromResult(list);
    }

    public static List<CommitInfo> GetCommits()
    {
        var output = RunGit("log", "--topo-order", "--pretty=%H%x09%s%x09%aN%x09%at%x09%P");
        if (output.Length == 0)
            return [];

        var list = new List<CommitInfo>(output.Length);
        foreach (var line in output)
        {
            var columns = line.Split('\t');
            if (columns.Length < 4)
                continue;

            if (!long.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTime))
                continue;

            list.Add(new CommitInfo(
                columns[0],
                columns[1],
                columns[2],
                DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime,
                columns[4].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ));
        }

        return list;
    }

    private static string[] RunGit(params string[] arguments)
    {
        if (string.IsNullOrEmpty(mainPath)) return [];

        using var process = StartGitProcess(arguments);

        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            return [];

        return SplitLines(output.Replace("\\ No newline at end of file", ""));
    }

    private static Process StartGitProcess(IReadOnlyCollection<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = mainPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
    }

    private static string[] SplitLines(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        return output
            .TrimEnd('\r', '\n')
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    private static int FindFirstDiffLine(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("@@", StringComparison.Ordinal))
                return i;
        }

        return lines.Length;
    }

    private static bool TryParseFileStatus(string line, out string file, out StatusFile status)
    {
        file = string.Empty;
        status = StatusFile.Modify;

        var columns = line.Split('\t', StringSplitOptions.TrimEntries);
        if (columns.Length < 2)
            return false;

        file = columns[^1];
        status = columns[0] switch
        {
            "A" => StatusFile.Add,
            "D" => StatusFile.Delete,
            _ => StatusFile.Modify
        };

        return true;
    }
}
