using Edi.AzureBlobSync.Interfaces;
using Spectre.Console;
using System.Reflection;
using System.Text;

namespace Edi.AzureBlobSync.Services;

public class ConsoleService : IConsoleService
{
    public void SetOutputEncoding(Encoding encoding) => Console.OutputEncoding = encoding;

    public void WriteMarkup(string markup) => AnsiConsole.Write(new Markup(markup));

    public void WriteLine(string message) => AnsiConsole.WriteLine(message);

    public void WriteException(Exception ex) => AnsiConsole.WriteException(ex);

    public ConsoleKeyInfo ReadKey(bool intercept = false) => Console.ReadKey(intercept);

    public void ReadKey() => Console.ReadKey();

    public bool Confirm(string message) => AnsiConsole.Confirm(message);

    public string Ask(string question) => AnsiConsole.Ask<string>(question);

    public async Task StartStatusAsync(string message, Func<Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(message, async _ => await action());
    }

    public void WriteTable(string title, Dictionary<string, string> data)
    {
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";

        var table = new Table
        {
            Title = new($"{title} {appVersion} | .NET {Environment.Version}")
        };

        table.AddColumn("Parameter");
        table.AddColumn("Value");

        foreach (var kvp in data)
        {
            table.AddRow($"[blue]{kvp.Key}[/]", kvp.Value);
        }

        AnsiConsole.Write(table);
    }

    public void WriteFileTable(IEnumerable<FileSyncInfo> files)
    {
        var table = new Table();
        table.AddColumn("File Name");
        table.AddColumn("Length (bytes)");
        table.AddColumn("Content-MD5");

        foreach (var file in files)
        {
            table.AddRow(
                file.FileName ?? "Unknown",
                file.Length?.ToString() ?? "Unknown",
                string.IsNullOrEmpty(file.ContentMD5) ? "Not calculated" : file.ContentMD5);
        }

        AnsiConsole.Write(table);
    }
}