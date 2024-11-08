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

## 免责申明

此项目(Azure-Blob-Sync)及其配套组件均为免费开源的产品，仅用于学习交流，并且不直接向中国提供服务，中国访客请于下载后立即删除。

任何中国境内的组织及个人不得使用此项目(Azure-Blob-Sync)及其配套组件构建任何形式的面向中国境内访客的网站或服务。

不可用于任何违反中华人民共和国(含台湾省)或使用者所在地区法律法规的用途。

因为作者即本人仅完成代码的开发和开源活动(开源即任何人都可以下载使用)，从未参与访客的任何运营和盈利活动。

且不知晓访客后续将程序源代码用于何种用途，故访客使用过程中所带来的任何法律责任即由访客自己承担。

[《开源软件有漏洞，作者需要负责吗？是的！》](https://go.edi.wang/aka/os251)
