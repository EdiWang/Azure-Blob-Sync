# Azure-Blob-Backup
.NET Core Tool for Backup files in Azure Blob Storage to local file system.

![.NET Core](https://github.com/EdiWang/Azure-Blob-Backup/workflows/.NET%20Core/badge.svg)

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
