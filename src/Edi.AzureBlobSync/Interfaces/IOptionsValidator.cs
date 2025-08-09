namespace Edi.AzureBlobSync.Interfaces;

public interface IOptionsValidator
{
    Options ValidateAndPrompt(Options options);
}