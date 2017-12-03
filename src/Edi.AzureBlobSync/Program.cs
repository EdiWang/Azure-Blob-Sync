using System;
using CommandLine;

namespace Edi.AzureBlobSync
{
    class Options
    {
        [Option('n', Required = true, HelpText = "Storage Account Name")]
        public string AccountName { get; set; }

        [Option('k', Required = true, HelpText = "Storage Account Key")]
        public string AccountKey { get; set; }

        [Option('p', Default = "C:\\AzureBlobSyncTemp", Required = true, HelpText = "Local Folder Path")]
        public string LocalFolderPath { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<Options>(args);
            if (parserResult.Tag == ParserResultType.Parsed)
            {
                var opts = ((Parsed<Options>)parserResult).Value;
                
                Console.WriteLine($"{opts.AccountKey} | {opts.AccountName} | {opts.LocalFolderPath}");
                Console.ReadLine();
            }
        }
    }
}
