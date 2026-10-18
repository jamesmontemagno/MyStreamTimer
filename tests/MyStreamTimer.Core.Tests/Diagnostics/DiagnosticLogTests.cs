using System.IO.Compression;
using System.Text.Json;
using MyStreamTimer.Core.Diagnostics;

namespace MyStreamTimer.Core.Tests.Diagnostics;

public sealed class DiagnosticLogTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), $"mst-diagnostics-test-{Guid.NewGuid():N}");

    [Fact]
    public void Records_are_bounded_valid_json_without_exception_messages()
    {
        var log = new DiagnosticLog(directory, maximumFiles: 3, maximumFileBytes: 2048);
        for (var i = 0; i < 40; i++)
            log.Write("test-error", new string('x', 1000), new IOException(@"PRIVATE C:\Users\secret\output.txt"));
        var files = Directory.GetFiles(directory);
        Assert.InRange(files.Length, 1, 3);
        foreach (var file in files)
        {
            Assert.InRange(new FileInfo(file).Length, 1, 2048);
            foreach (var line in File.ReadLines(file))
            {
                using var entry = JsonDocument.Parse(line);
                Assert.Equal("System.IO.IOException", entry.RootElement.GetProperty("ExceptionType").GetString());
                Assert.DoesNotContain("PRIVATE", line);
                Assert.DoesNotContain("secret", line);
            }
        }
    }

    [Fact]
    public void Thrown_inner_exception_stacks_exclude_messages_and_source_paths()
    {
        Exception captured;
        try
        {
            throw new IOException(@"PRIVATE C:\Users\secret\output.txt");
        }
        catch (IOException exception)
        {
            captured = new InvalidOperationException("PRIVATE wrapper", exception);
        }
        var log = new DiagnosticLog(directory);
        log.Write("nested", exception: captured);
        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(directory)));
        Assert.Contains("DiagnosticLogTests.Thrown_inner_exception", text);
        Assert.Contains("System.IO.IOException", text);
        Assert.DoesNotContain("PRIVATE", text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain(".cs:", text);
    }

    [Fact]
    public void Export_excludes_legacy_crash_logs_and_unrelated_files()
    {
        var log = new DiagnosticLog(directory);
        log.Write("startup", "test-build");
        File.WriteAllText(Path.Combine(directory, "crash-old.txt"), "private exception message");
        File.WriteAllText(Path.Combine(directory, "settings.dat"), "private settings");
        var destination = Path.Combine(directory, "report.zip");
        File.WriteAllText(destination, "");
        Assert.True(log.TryExport(destination, out var error));
        Assert.Null(error);
        using var archive = ZipFile.OpenRead(destination);
        Assert.Single(archive.Entries);
        Assert.StartsWith("diagnostics-", archive.Entries[0].Name);
        using var reader = new StreamReader(archive.Entries[0].Open());
        Assert.DoesNotContain("private", reader.ReadToEnd());
    }

    [Fact]
    public void Oversized_export_is_rejected_before_overwriting_the_destination()
    {
        var log = new DiagnosticLog(directory, maximumFileBytes: 1024);
        log.Write("startup");
        File.WriteAllText(Path.Combine(directory, "diagnostics-oversized.jsonl"), new string('x', 1025));
        var destination = Path.Combine(directory, "report.zip");
        File.WriteAllText(destination, "existing report");

        Assert.False(log.TryExport(destination, out var error));

        Assert.Contains("size limit", error);
        Assert.Equal("existing report", File.ReadAllText(destination));
    }

    [Fact]
    public void Unwritable_export_reports_failure_without_throwing()
    {
        var log = new DiagnosticLog(directory);
        log.Write("startup");
        var destination = Path.Combine(directory, "not-a-folder");
        File.WriteAllText(destination, "");

        Assert.False(log.TryExport(Path.Combine(destination, "report.zip"), out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_empty_logs_do_not_create_an_empty_report(bool createDirectory)
    {
        var log = new DiagnosticLog(directory);
        if (createDirectory)
            Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "report.zip");

        Assert.False(log.TryExport(destination, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void Io_failures_are_not_silently_reported_as_success()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(file, "");
        var log = new DiagnosticLog(file);
        Assert.Throws<IOException>(() => log.Write("startup"));
    }

    [Fact]
    public void Concurrent_writers_keep_complete_records()
    {
        var log = new DiagnosticLog(directory);
        Parallel.For(0, 100, _ => log.Write("parallel"));
        var lines = Directory.GetFiles(directory).SelectMany(File.ReadLines).ToArray();
        Assert.Equal(100, lines.Length);
        foreach (var line in lines)
        {
            using var record = JsonDocument.Parse(line);
            Assert.Equal("parallel", record.RootElement.GetProperty("Event").GetString());
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
