namespace Edi.AzureBlobSync.Interfaces;

public interface IOptionsValidator
{
    Task<Options> ValidateAndPromptAsync(Options options);
}