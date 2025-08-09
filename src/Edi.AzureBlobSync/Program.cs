using System.Text;
using CommandLine;
using Edi.AzureBlobSync.Interfaces;
using Edi.AzureBlobSync.Services;

namespace Edi.AzureBlobSync;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Setup dependencies
        var consoleService = new ConsoleService();
        var blobService = new BlobService();
        var fileService = new FileService();
        var optionsValidator = new OptionsValidator(consoleService);
        var syncService = new AzureBlobSyncService(blobService, fileService, consoleService, optionsValidator);

        consoleService.SetOutputEncoding(Encoding.UTF8);

        var parserResult = Parser.Default.ParseArguments<Options>(args);

        if (parserResult is Parsed<Options> parsedResult)
        {
            return await syncService.RunAsync(parsedResult.Value);
        }
        else
        {
            consoleService.WriteMarkup("[red]ERROR: Failed to parse console parameters.[/]");
            consoleService.ReadKey();
            return 1;
        }
    }
}
