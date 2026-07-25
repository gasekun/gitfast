using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using gitFast.Models;
using FileCommit = gitFast.Models.FileCommit;

namespace gitFast.Services;

public static class GitService
{
    public static string MainPath { get; private set; } = @"";

    public static void SetMainPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Repository path cannot be empty.", nameof(path));

        MainPath = path;
    }

    public static async Task<string[]> GetFileDiff(string id, string file)
    {
        var output = await RunGit("show", id, "--format=", "--", file);
        if (output.Length == 0)
            return [];

        var startIndex = FindFirstDiffLine(output);
        return startIndex >= output.Length ? [] : output[startIndex..];
    }

    public static async Task<(int add, int remove)> GetFileEditCount(string id, string file)
    {
        var output = await RunGit("show", id, "--numstat", "--format=", "--", file);
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

    public static async Task<List<FileCommit>> GetFilesCommit(string id)
    {
        var output = await RunGit("show", id, "--format=", "--name-status");
        
        if (output.Length == 0)
            return [];

        var regex = new Regex(@"\\(\d+)\\(\d+)");
        var list = new List<FileCommit>(output.Length);
        foreach (var line in output)
        {
            if (!TryParseFileStatus(line, out var file, out var status))
                continue;

            if (file.Contains("\""))
            {
                file = file.Replace("\"", "");

                while (regex.IsMatch(file))
                {
                    var match = regex.Match(file);
                    var data = OctalToDecimal(int.Parse(match.Groups[1].Value)).ToString("x2") +
                               OctalToDecimal(int.Parse(match.Groups[2].Value)).ToString("x2");

                    var hex = Convert.FromHexString(data);

                    var str = Encoding.UTF8.GetString(hex);
                    file = file.Replace(match.Value, str);
                }
            }

            list.Add(new FileCommit(file, status));
        }

        return list;
    }

    public static async Task<List<CommitInfo>> GetCommits()
    {
        //"--all", 
        var output = await RunGit("log", "--topo-order", "--pretty=%H%x09%s%x09%aN%x09%at%x09%P%x09%D");
        
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

            var commit = new CommitInfo(
                columns[0],
                columns[1],
                columns[2],
                DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime,
                columns[4].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            );
            list.Add(commit);

            if (!string.IsNullOrEmpty(columns[5]))
            {
                var tags = columns[5].Split(",", StringSplitOptions.TrimEntries);

                foreach (var tag in tags)
                {
                    if (tag.Contains("->"))
                    {
                        commit.Tags.Add(new Tag(TagType.Branch, tag.Replace("HEAD -> ", "")));
                    }
                    else if (tag.Contains("/"))
                    {
                        if (!tag.Contains("HEAD"))
                            commit.Tags.Add(new Tag(TagType.RemoteBranch, tag));
                    }
                    else if (tag.Contains("tag"))
                    {
                        commit.Tags.Add(new Tag(TagType.Tag, tag.Replace("tag: ", "")));
                    }
                    else
                    {
                        commit.Tags.Add(new Tag(TagType.Branch, tag));
                    }
                }

                commit.Tags.Sort((item1, item2) => item1.Type.CompareTo(item2.Type));
            }
        }

        return list;
    }

    public static async Task GitFetch()
    {
        await RunGit("fetch", "--all");
        await RunGit("pull", "--all");
    }

    public static async Task<List<BranchInfo>> GetBranches(bool isLocal = true)
    {
        var output = (isLocal
                ? await RunGit("branch")
                : await RunGit("branch", "-r"))
            .Select(item => item.Replace("origin/HEAD ->", ""))
            .Select(item => new BranchInfo(item.Replace("*", "").Trim(), item.Contains('*')));

        return output.ToList();
    }

    public static async Task<List<string>> GetTags()
    {
        return (await RunGit("tag")).ToList();
    }

    public static async Task SelectBranch(string branch)
    {
        if (branch.Contains("origin/"))
        {
            await RunGit("switch", "-c", branch.Replace("origin/", ""), branch);
        }
        else
        {
            await RunGit("switch", branch);
        }
    }

    public static async Task<string> GetHeadBranch(string branch)
    {
        return (await RunGit("rev-parse", branch))[0];
    }

    public static async Task RemoveBranch(string branchName)
    {
        await RunGit("branch", "-D", branchName);
    }

    private static async Task<string[]> RunGit(params string[] arguments)
    {
        if (string.IsNullOrEmpty(MainPath)) return [];

        using var process = StartGitProcess(arguments);

        var output = await process.StandardOutput.ReadToEndAsync();
        _ = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            return [];

        return SplitLines(output.Replace("\\ No newline at end of file", ""));
    }

    private static Process StartGitProcess(IReadOnlyCollection<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = MainPath,
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
            .Split(["\r\n", "\n"], StringSplitOptions.None);
    }

    private static int FindFirstDiffLine(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("@@", StringComparison.Ordinal))
                return i;

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

    private static int OctalToDecimal(int n)
    {
        int num = n;
        int dec_value = 0;

        // Initializing base value  
        // to 1, i.e 8^0 
        int b_ase = 1;

        int temp = num;
        while (temp > 0)
        {
            // Extracting last digit 
            int last_digit = temp % 10;
            temp = temp / 10;

            // Multiplying last digit  
            // with appropriate base  
            // value and adding it to  
            // dec_value 
            dec_value += last_digit * b_ase;

            b_ase = b_ase * 8;
        }

        return dec_value;
    }
}