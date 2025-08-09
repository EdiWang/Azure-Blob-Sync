using Edi.AzureBlobSync.Interfaces;

namespace Edi.AzureBlobSync.Services;

public class OptionsValidator : IOptionsValidator
{
    private readonly IConsoleService _consoleService;

    public OptionsValidator(IConsoleService consoleService)
    {
        _consoleService = consoleService;
    }

    public async Task<Options> ValidateAndPromptAsync(Options options)
    {
        options.ConnectionString ??= _consoleService.Ask("Enter Azure Storage Account connection string: ");
        options.Container ??= _consoleService.Ask("Enter container name: ");
        options.Path ??= _consoleService.Ask("Enter local path: ");

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