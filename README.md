# Azure Blob Sync
[![.NET Build and Pack](https://github.com/EdiWang/Azure-Blob-Sync/actions/workflows/dotnet.yml/badge.svg)](https://github.com/EdiWang/Azure-Blob-Sync/actions/workflows/dotnet.yml)

.NET Tool for backup files in Azure Blob Storage to local file system.

You may also checkout [AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-blobs-synchronize?WT.mc_id=AZ-MVP-5002809) from Microsoft that does the same job much more faster.

## Install

Prerequests: 

- .NET 8.0 SDK 

```bash
dotnet tool install --global edi.azureblobsync
```

## Usage

Example

```bash
azblobsync --connection "DefaultEndpointsProtocol=https;AccountName=*******;AccountKey==*******;EndpointSuffix=core.windows.net" --container "attachments" --path "D:\Backup\attachments"
```

### Parameters

```
--connection	Azure Storage Account Connection String (Required)
--container	Container Name (Required)
--path		Local Folder Path (Required, Default: C:\AzureBlobSyncTemp)
--threads	Download threads (Default: 10)
--silence	Fully automated silence mode (Default: false)
--comparehash   Compare file hash to determine whether to download (Default: true)
--keepold       Keep local old file versions, do not override when receving a new version of file from Azure (Default: false)
--help		Display this help screen.
--version	Display version information.
```

![image](https://raw.githubusercontent.com/EdiWang/Azure-Blob-Backup/master/screenshots/sc2.png)

