# Copilot Instructions

## Project Overview

This repository contains `Edi.AzureBlobSync`, a .NET global tool named `azblobsync`. It backs up files from an Azure Blob Storage container to a local folder.

- Main project: `src/Edi.AzureBlobSync/Edi.AzureBlobSync.csproj`
- Tests: `src/Edi.AzureBlobSync.Tests/Edi.AzureBlobSync.Tests.csproj`
- Target framework: `net10.0`
- CLI stack: `System.CommandLine`
- Console UI: `Spectre.Console`
- Azure SDK: `Azure.Storage.Blobs`
- Test stack: xUnit v3 and Moq

## Architecture Guidelines

- Keep `Program.cs` thin. It should parse CLI options, create service instances, and call `AzureBlobSyncService.RunAsync`.
- Put orchestration logic in `AzureBlobSyncService`; keep Azure SDK calls in `BlobService`, local file system operations in `FileService`, terminal interaction in `ConsoleService`, and option validation in `OptionsValidator`.
- Prefer existing interfaces in `src/Edi.AzureBlobSync/Interfaces` when adding testable behavior around Azure, console, or file system operations.
- Preserve async APIs for Azure/network operations and avoid blocking on tasks with `.Result` or `.Wait()`.
- Use dependency injection by constructor parameters, matching the existing primary-constructor style where it is already used.
- Keep public CLI option names stable unless the README and tests are updated at the same time.

## Sync Behavior To Preserve

- `--connection`, `--container`, and `--path` are prompted interactively when missing.
- `OptionsValidator` validates connection strings by requiring `AccountName=` and `AccountKey=`, and converts relative local paths to absolute paths.
- File comparison uses `FileSyncInfoComparer`: case-insensitive file name, length, and ordinal `ContentMD5` comparison.
- When `--comparehash` is false, content hashes should be empty strings and comparison should rely on file name and length.
- Azure Blob archive-tier items are counted and skipped rather than downloaded.
- `--keepold` preserves an existing local file by moving it to a timestamped name before downloading a replacement.
- `--silence` should avoid confirmation prompts and key pauses so the tool can run unattended.
- Be careful with path behavior: local file enumeration currently uses `SearchOption.TopDirectoryOnly`, while blob downloads can create nested directories for blob names containing folder separators.

## Coding Style

- Follow the existing C# style: file-scoped namespaces, implicit usings, `var` where the type is obvious, collection expressions such as `[]` where appropriate, and concise service classes.
- Keep comments sparse and useful. Do not add comments that merely restate the code.
- Do not log secrets or print full Azure Storage connection strings.
- Keep Spectre.Console markup valid and escape or avoid user-controlled markup when necessary.
- Prefer small focused changes over broad refactors, especially in the CLI surface and sync behavior.

## Testing Guidelines

- Add or update tests in `src/Edi.AzureBlobSync.Tests` for behavior changes.
- Use xUnit `[Fact]` tests with Arrange/Act/Assert sections, following the existing naming pattern: `MethodName_Condition_ExpectedResult`.
- Use Moq for interface collaborators such as `IConsoleService`.
- For Azure SDK listing behavior, mock `BlobContainerClient.GetBlobsAsync` with `AsyncPageable<T>` and `BlobsModelFactory`, as shown in `BlobServiceTests`.
- Avoid tests that require a real Azure Storage account, real secrets, or live network access.
- Clean up temporary directories and files in `IDisposable.Dispose` when tests touch the file system.

## Build And Validation

Use these commands from the repository root unless a task provides a narrower project-specific command:

```bash
dotnet build src/Edi.AzureBlobSync.slnx
dotnet test src/Edi.AzureBlobSync.slnx
```

For package-related changes, also validate packing from `src`:

```bash
dotnet pack --configuration Release
```

The GitHub Actions workflow builds, tests, packs, and pushes from the `src` directory on `master` using .NET 10.0.x.

## Documentation

- Update `README.md` when CLI options, defaults, install instructions, or behavior visible to users changes.
- Keep examples free of real account names, keys, SAS tokens, or connection strings.