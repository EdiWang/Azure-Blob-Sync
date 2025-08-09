using Edi.AzureBlobSync.Interfaces;

namespace Edi.AzureBlobSync.Services;

public class OptionsValidator(IConsoleService consoleService) : IOptionsValidator
{
    public Options ValidateAndPrompt(Options options)
    {
        options.ConnectionString ??= consoleService.Ask("Enter Azure Storage Account connection string: ");
        options.Container ??= consoleService.Ask("Enter container name: ");
        options.Path ??= consoleService.Ask("Enter local path: ");

        // Validate connection string format
        if (!options.ConnectionString.Contains("AccountName=") || !options.ConnectionString.Contains("AccountKey="))
        {
            throw new ArgumentException("Invalid connection string format.");
        }

        // Ensure path is absolute
        if (!Path.IsPathRooted(options.Path))
        {
            options.Path = Path.GetFullPath(options.Path);
        }

        return options;
    }
}