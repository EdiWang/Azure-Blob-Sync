# Azure-Blob-Backup
.NET Core Tool for Backup files in Azure Blob Storage to local file system.

[![Build status](https://dev.azure.com/ediwang/EdiWang-GitHub-Builds/_apis/build/status/Azure-Blob-Backup-CI)](https://dev.azure.com/ediwang/EdiWang-GitHub-Builds/_build/latest?definitionId=-1)

## Install

```
dotnet tool install --global edi.azureblobsync
```

## Usage

### Parameters

```
-s           Required. Connection String
-c           Required. Blob Container Name
-p           Required. (Default: C:\AzureBlobSyncTemp) Local Folder Path
--help       Display this help screen.
--version    Display version information.
```

![image](https://raw.githubusercontent.com/EdiWang/Azure-Blob-Backup/master/screenshots/sc1.png)
