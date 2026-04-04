using Edi.AzureBlobSync.Services;
using System.CommandLine;
using System.Text;

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

        var connectionOption = new Option<string>("--connection") { Description = "Storage Account Connection String" };
        var containerOption = new Option<string>("--container") { Description = "Blob Container Name" };
        var pathOption = new Option<string>("--path") { Description = "Local Folder Path" };
        var threadsOption = new Option<int>("--threads") { Description = "Download threads", DefaultValueFactory = _ => 10 };
        var silenceOption = new Option<bool>("--silence") { Description = "Silence mode" };
        var keepOldOption = new Option<bool>("--keepold") { Description = "Keep local old file versions, do not override when receiving a new version of file from Azure" };
        var compareHashOption = new Option<bool>("--comparehash") { Description = "Compare file hash", DefaultValueFactory = _ => true };

        var rootCommand = new RootCommand(".NET Tool for backup files in Azure Blob Storage to local file system.")
        {
            connectionOption,
            containerOption,
            pathOption,
            threadsOption,
            silenceOption,
            keepOldOption,
            compareHashOption
        };

        rootCommand.SetAction(async (ParseResult parseResult) =>
        {
            var options = new Options
            {
                ConnectionString = parseResult.GetValue(connectionOption),
                Container = parseResult.GetValue(containerOption),
                Path = parseResult.GetValue(pathOption),
                Threads = parseResult.GetValue(threadsOption),
                Silence = parseResult.GetValue(silenceOption),
                KeepOld = parseResult.GetValue(keepOldOption),
                CompareHash = parseResult.GetValue(compareHashOption)
            };

            return await syncService.RunAsync(options);
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
