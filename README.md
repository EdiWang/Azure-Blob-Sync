# Azure Blob Sync
![.NET](https://github.com/EdiWang/Azure-Blob-Backup/workflows/.NET%20Core/badge.svg)

.NET Tool for backup files in Azure Blob Storage to local file system.

## Install

Prerequests: 

- .NET 6.0 SDK 

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
--help		Display this help screen.
--version	Display version information.
```

![image](https://raw.githubusercontent.com/EdiWang/Azure-Blob-Backup/master/screenshots/sc2.png)
