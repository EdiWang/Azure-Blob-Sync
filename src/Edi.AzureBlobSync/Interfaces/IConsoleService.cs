using System.Text;

namespace Edi.AzureBlobSync.Interfaces;

public interface IConsoleService
{
    void SetOutputEncoding(Encoding encoding);
    void WriteMarkup(string markup);
    void WriteLine(string message);
    void WriteException(Exception ex);
    ConsoleKeyInfo ReadKey(bool intercept = false);
    void ReadKey();
    bool Confirm(string message);
    string Ask(string question);
    Task StartStatusAsync(string message, Func<Task> action);
    void WriteTable(string title, Dictionary<string, string> data);
    void WriteFileTable(IEnumerable<FileSyncInfo> files);
}