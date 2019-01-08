# Azure-Blob-Backup
Backup files in Azure Blob Storage to local file system.

[![Build status](https://dev.azure.com/ediwang/EdiWang-GitHub-Builds/_apis/build/status/Azure-Blob-Backup-CI)](https://dev.azure.com/ediwang/EdiWang-GitHub-Builds/_build/latest?definitionId=-1)

## Usage

### Parameters

```
-n           Required. Storage Account Name
-k           Required. Storage Account Key
-c           Required. Blob Container Name
-p           Required. (Default: C:\AzureBlobSyncTemp) Local Folder Path
--help       Display this help screen.
--version    Display version information.
```

### Example

`
-n editeststorage -k TRVxchJaH+UX/FAjeqs2+SGosPQ0lZ82DxNpuc2BESn31gugx3fN3uHcP0WAfiD97CPN8AWP5Jpji9Kx5HoX+A== -c abstest -p C:\AzureBlobSyncTarget
`

![image](https://raw.githubusercontent.com/EdiWang/Azure-Blob-Backup/master/screenshots/sc1.png)
