# XiHan.Framework.ObjectStorage

> 对象存储统一抽象：把「文件存到哪里」抽象成 `IFileStorageProvider`，本地磁盘与阿里云 OSS / MinIO / 腾讯云 COS 四种后端实现同一套上传、下载、删除、预签名 URL 等操作，并支持多提供程序管理与业务路由。

- **NuGet**：`XiHan.Framework.ObjectStorage`
- **模块类**：`XiHanObjectStorageModule`
- **所在层**：基础设施层
- **关键依赖**：第三方存储 SDK `Aliyun.OSS.SDK.NetCore`（阿里云 OSS）、`Minio`（MinIO）、`Tencent.QCloud.Cos.Sdk`（腾讯云 COS）；框架内部仅依赖 `XiHan.Framework.Core`。

## 概述

XiHan.Framework.ObjectStorage 把「文件存到哪里」抽象成统一接口 `IFileStorageProvider`：无论底层是本地磁盘、阿里云 OSS、MinIO 还是腾讯云 COS，业务代码都用同一套上传、下载、删除、存在性检查、元数据读取、复制/移动、目录列举、预签名 URL 与分片上传方法。四种后端各自封装对应的第三方 SDK，统一继承抽象基类 `FileStorageProviderBase`。

在此之上，`IFileStorageProviderManager` 负责「按名称取出提供程序」（带缓存），`IFileStorageRouter` 负责「按业务路由键选择提供程序」。二者组合，让你可以同时启用多个后端，并把不同类型的文件（如头像、附件）路由到不同存储，本地开发用磁盘、上线切云端只改配置不改代码。

## 何时使用

- 需要文件上传/下载能力，但不想把代码绑死在某一家云厂商上。
- 本地开发用磁盘存储、上线切换到 OSS/COS/MinIO，只改配置不改代码。
- 需要分片上传、断点续传、预签名临时访问 URL、文件复制/移动、目录列举等常见对象存储操作。
- 需要按业务把不同文件路由到不同存储桶或不同提供程序。
- 与 [XiHan.Framework.VirtualFileSystem](./virtual-file-system) 的区别：VirtualFileSystem 面向「本地物理目录 + 程序集嵌入资源」的统一虚拟路径**只读访问**（配置、模板、静态资源），不含云存储；ObjectStorage 才是面向对象存储/云存储的文件**读写与生命周期管理**。

## 安装与启用

```bash
dotnet add package XiHan.Framework.ObjectStorage
```

```csharp
[DependsOn(typeof(XiHanObjectStorageModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用 `services.AddXiHanObjectStorage(config)`，据此完成：

- 绑定五个配置节到对应 Options：`XiHan:ObjectStorage`（全局）、`:Local`、`:Minio`、`:AliyunOss`、`:TencentCos`。
- `TryAddSingleton` 注册 `IFileStorageProviderManager`（默认 `DefaultFileStorageProviderManager`）与 `IFileStorageRouter`（默认 `DefaultFileStorageRouter`）。
- 据全局配置的 `EnabledProviders` + `DefaultProvider` 自动注册被启用的 Provider（`TryAddSingleton` 具体类型 + 登记到 `XiHanObjectStorageProviderOptions`）。若两者都为空，回退到本地存储（`Local`）。

> Provider 是惰性构造并缓存的：`DefaultFileStorageProviderManager` 首次 `GetProvider(name)` 时从容器解析出实例并放入 `ConcurrentDictionary` 缓存，后续复用。

## 工作原理

- **注册阶段**（`RegisterConfiguredProviders`）：把 `EnabledProviders` 与 `DefaultProvider` 汇成一个大小写不敏感集合（空则加 `Local`），逐个按名称（`LOCAL` / `MINIO` / `ALIYUNOSS` / `TENCENTCOS`，其它值抛 `InvalidOperationException`）调用对应 `AddXxxFileStorageProvider` 扩展。每个扩展 `TryAddSingleton<TProvider>` 并把 `providerName → typeof(TProvider)` 登记进 `XiHanObjectStorageProviderOptions`。
- **解析阶段**（`IFileStorageProviderManager.GetProvider`）：入参 provider 名为空时回退到 `XiHanObjectStorageOptions.DefaultProvider`；查 `ProviderTypes` 拿到类型，未注册则抛异常；用 `ConcurrentDictionary.GetOrAdd` 从容器解析并缓存实例。
- **路由阶段**（`IFileStorageRouter.ResolveProviderName`）：显式 `providerName` 优先级最高；否则按 `routeKey` 查 `RouteProviderMappings`（大小写不敏感）命中即用；未命中时——`StrictRouteMatch=true` 抛异常，否则回退 `DefaultProvider`。
- **上传统一计时**：`FileStorageProviderBase.UploadAsync` 包裹子类 `UploadCoreAsync`，统一计时（`DurationMs`）并把异常收敛为 `FileUploadResult { Success=false, ErrorMessage=... }`（不抛出）。

## 核心能力

- **统一存储抽象**：`IFileStorageProvider` 定义上传、下载、删除、存在性检查、获取元数据、复制、移动、目录列举、预签名 URL 与分片上传全套操作，四种后端实现同一接口。
- **能力自描述**：每个 Provider 通过 `SupportChunkedUpload` / `SupportResumableUpload` 声明是否支持分片/断点续传。基类默认二者为 `false`，不支持的方法调用会抛 `NotSupportedException`。
- **多提供程序管理**：`IFileStorageProviderManager` 按名称取出提供程序（带缓存），可 `TryGetProvider` 安全获取、`GetRegisteredProviderNames` 查询已注册名单。
- **业务路由**：`IFileStorageRouter` 按业务路由键（如 `avatar`、`attachment`）解析并选择目标提供程序，支持严格匹配开关 `StrictRouteMatch`。
- **分片与断点续传**：`InitiateChunkedUploadAsync` / `UploadChunkAsync` / `CompleteChunkedUploadAsync` / `AbortChunkedUploadAsync` 覆盖大文件分片上传全流程。
- **预签名 URL**：`GeneratePresignedUrlAsync(path, expiresIn)` 生成带过期时间的临时访问链接（云存储提供真实签名；本地存储直接返回静态可访问 URL）。
- **本地存储直链**：本地提供程序默认落在 `wwwroot/Uploads` 下，配合 Web.Api 静态文件服务可直接通过 URL 前缀访问（见「配置」）。

## 主要 API / 类型

### 契约与基类

| 类型 | 说明 |
| --- | --- |
| `IFileStorageProvider` | 存储提供程序接口，全部读写操作的统一契约 |
| `FileStorageProviderBase` | 抽象基类：统一上传计时/异常收敛、默认「不支持分片/预签名」实现、`NormalizePath`/`GetFileExtension`/`ComputeFileHashAsync`（MD5）等工具 |
| `IFileStorageProviderManager` / `DefaultFileStorageProviderManager` | 按名称获取提供程序（带缓存），支持 `TryGetProvider`、`GetRegisteredProviderNames` |
| `IFileStorageRouter` / `DefaultFileStorageRouter` | 按业务路由键选择提供程序，`ResolveProviderName` / `Route` |
| `ObjectStorageProviderNames` | 提供程序名称常量：`Local="Local"`、`Minio="MinIO"`、`AliyunOss="AliyunOSS"`、`TencentCos="TencentCOS"` |

### `IFileStorageProvider` 关键方法签名

| 方法 | 说明 |
| --- | --- |
| `string ProviderName { get; }` | 提供程序名称 |
| `bool SupportChunkedUpload { get; }` / `bool SupportResumableUpload { get; }` | 能力声明 |
| `Task<FileUploadResult> UploadAsync(FileUploadRequest request, ...)` | 上传文件 |
| `Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, ...)` | 初始化分片上传，返回 uploadId |
| `Task<ChunkUploadResult> UploadChunkAsync(ChunkUploadRequest request, ...)` | 上传单个分片 |
| `Task<FileUploadResult> CompleteChunkedUploadAsync(ChunkedUploadCompleteRequest request, ...)` | 合并完成分片上传 |
| `Task AbortChunkedUploadAsync(string uploadId, ...)` | 取消分片上传 |
| `Task<Stream> DownloadAsync(string path, ...)` | 下载文件为流 |
| `Task DeleteAsync(string path, ...)` / `Task DeleteAsync(string path, string? bucketName, ...)` | 删除文件（可指定桶） |
| `Task<bool> ExistsAsync(string path, ...)` / `(string path, string? bucketName, ...)` | 存在性检查 |
| `Task<FileMetadata> GetMetadataAsync(string path, ...)` / `(string path, string? bucketName, ...)` | 读取元数据 |
| `Task<string> GeneratePresignedUrlAsync(string path, TimeSpan expiresIn, ...)` | 生成预签名 URL |
| `Task CopyAsync(string sourcePath, string destinationPath, ...)` | 复制文件 |
| `Task MoveAsync(string sourcePath, string destinationPath, ...)` | 移动文件 |
| `Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, ...)` | 列出目录下文件 |

### 提供程序实现与能力对照

| 实现类 | `ProviderName` | 分片上传 | 断点续传 | 预签名 URL |
| --- | --- | --- | --- | --- |
| `LocalFileStorageProvider` | `"Local"` | ✅ | ✅ | 返回静态直链（`expiresIn` 被忽略） |
| `AliyunOssStorageProvider` | `"AliyunOSS"` | ✅ | ✅ | ✅（真实签名） |
| `MinioFileStorageProvider` | `"MinIO"` | ❌ | ❌ | ✅（真实签名） |
| `TencentCosStorageProvider` | `"TencentCOS"` | ✅ | ✅ | ✅（真实签名） |

> 以源码为准：`MinioFileStorageProvider` 当前**不声明**分片/断点续传（`SupportChunkedUpload => false`），对其调用分片方法会走基类默认实现抛 `NotSupportedException`。上传大文件前请先检查目标 Provider 的 `SupportChunkedUpload`。

### 数据模型

| 类型 | 关键字段 |
| --- | --- |
| `FileUploadRequest` | `FileStream`、`FileName`、`StoragePath`、`ContentType?`、`BucketName?`、`Overwrite`、`AccessControl`（默认 `"private"`）、`Metadata?`、`CacheControl?`、`ProgressCallback?` |
| `FileUploadResult` | `Success`、`Path?`、`FullPath?`、`Url?`、`FileSize`、`ETag?`、`DurationMs`、`ErrorMessage?`、`Extra?` |
| `FileMetadata` | `Name?`、`Path?`、`Size`、`ContentType?`、`LastModified?`、`ETag?`、`IsDirectory`、`Url?`、`Metadata?` |
| `ChunkedUploadInitRequest` | `FileName`、`StoragePath`、`TotalSize`、`ChunkSize`（默认 5MB）、`ContentType?`、`BucketName?`、`AccessControl`、`Metadata?` |
| `ChunkUploadRequest` | `UploadId`、`StoragePath`、`ChunkNumber`（从 1 起）、`ChunkData`、`ChunkSize`、`TotalSize`、`TotalChunks`、`ChunkMd5?`、`BucketName?` |
| `ChunkUploadResult` | `Success`、`ChunkNumber`、`ETag?`、`ErrorMessage?` |
| `ChunkedUploadCompleteRequest` / `ChunkInfo` | `UploadId`、`StoragePath`、`ChunkInfos`（`ChunkNumber` + `ETag?`）、`BucketName?` |

## 配置

全局配置节 `XiHan:ObjectStorage`（`XiHanObjectStorageOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `DefaultProvider` | `string` | `"Local"` | 默认存储提供程序名称 |
| `EnabledProviders` | `string[]` | `["Local"]` | 启用的存储提供程序列表 |
| `RouteProviderMappings` | `Dictionary<string, string>` | 空（大小写不敏感） | 业务路由键 → 提供程序名 |
| `StrictRouteMatch` | `bool` | `false` | 有路由键但未命中映射时是否抛异常 |

本地存储 `XiHan:ObjectStorage:Local`（`LocalStorageOptions`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `RootPath` | `string` | `"wwwroot/Uploads"` | 本地存储根目录（默认置于 Web 根 `wwwroot` 下，便于静态托管与直链） |
| `UrlPrefix` | `string` | `"/uploads"` | 生成访问 URL 时的路径前缀 |

> 本地存储配置节 `XiHan:ObjectStorage:Local`（`RootPath` / `UrlPrefix`）也被 **Web.Api** 用于挂载静态文件服务：`XiHanWebApiModule` 在鉴权中间件之前经 `IConfiguration` 读取这两个字段，以 `UseStaticFiles` + `PhysicalFileProvider` 把该目录映射到 `UrlPrefix` 请求路径，使本地存储返回的静态 URL（头像、公开文件等）可匿名直链访问。请显式配置 `RootPath`：对象存储默认值是 `wwwroot/Uploads`，而 Web.Api 完全无配置时仍回退 `wwwroot/uploads`；大小写敏感系统会把它们视为不同目录。

云存储 `XiHan:ObjectStorage:AliyunOss`（`AliyunOssStorageOptions`）：`AccessKeyId`、`AccessKeySecret`、`Endpoint`、`DefaultBucket`、`CdnDomain?`、`UseInternal`（默认 `false`）。

云存储 `XiHan:ObjectStorage:Minio`（`MinioStorageOptions`）：`Endpoint`、`AccessKey`、`SecretKey`、`DefaultBucket`、`UseSSL`（默认 `false`）、`Region?`。

云存储 `XiHan:ObjectStorage:TencentCos`（`TencentCosStorageOptions`）：`SecretId`、`SecretKey`、`AppId`、`Region`、`DefaultBucket`、`CdnDomain?`。

示例 `appsettings.json`：

```json
{
  "XiHan": {
    "ObjectStorage": {
      "DefaultProvider": "Local",
      "EnabledProviders": [ "Local", "AliyunOSS" ],
      "RouteProviderMappings": {
        "avatar": "Local",
        "attachment": "AliyunOSS"
      },
      "StrictRouteMatch": false,
      "Local": {
        "RootPath": "wwwroot/Uploads",
        "UrlPrefix": "/uploads"
      },
      "AliyunOss": {
        "AccessKeyId": "your-access-key-id",
        "AccessKeySecret": "your-access-key-secret",
        "Endpoint": "oss-cn-hangzhou.aliyuncs.com",
        "DefaultBucket": "your-bucket",
        "CdnDomain": null,
        "UseInternal": false
      },
      "Minio": {
        "Endpoint": "minio.example.com:9000",
        "AccessKey": "minioadmin",
        "SecretKey": "minioadmin",
        "DefaultBucket": "your-bucket",
        "UseSSL": false,
        "Region": null
      },
      "TencentCos": {
        "SecretId": "your-secret-id",
        "SecretKey": "your-secret-key",
        "AppId": "your-app-id",
        "Region": "ap-guangzhou",
        "DefaultBucket": "your-bucket-appid",
        "CdnDomain": null
      }
    }
  }
}
```

> 配置节大小写：全局节键为 `AliyunOss` / `Minio` / `TencentCos`（与各 Options 的 `SectionName` 一致），而提供程序**名称**常量是 `AliyunOSS` / `MinIO` / `TencentCOS`（用于 `EnabledProviders`、`DefaultProvider`、`RouteProviderMappings` 的值）。二者写法不同，别混用。

## 使用示例

### 上传文件并取得访问 URL

```csharp
public class AvatarService
{
    private readonly IFileStorageRouter _router;

    public AvatarService(IFileStorageRouter router) => _router = router;

    public async Task<string?> UploadAvatarAsync(Stream fileStream, string fileName)
    {
        // 按业务路由键 "avatar" 选择提供程序（据 RouteProviderMappings，未命中回退默认）
        var provider = _router.Route(routeKey: "avatar");

        var result = await provider.UploadAsync(new FileUploadRequest
        {
            FileStream = fileStream,
            FileName = fileName,
            StoragePath = $"avatars/{Guid.NewGuid():N}/{fileName}",
            ContentType = "image/png",
            AccessControl = "public-read",
            Overwrite = true
        });

        // result.Url 即可用于前端展示；本地存储下为 /uploads/... 静态直链
        return result.Success ? result.Url : null;
    }
}
```

### 按名称取用指定提供程序 + 生成预签名 URL

```csharp
public class DownloadService
{
    private readonly IFileStorageProviderManager _manager;

    public DownloadService(IFileStorageProviderManager manager) => _manager = manager;

    public async Task<string> GetTempLinkAsync(string path)
    {
        // 显式取阿里云 OSS 提供程序（名称用 ObjectStorageProviderNames.AliyunOss）
        var provider = _manager.GetProvider(ObjectStorageProviderNames.AliyunOss);

        // 生成 15 分钟有效的临时访问链接
        return await provider.GeneratePresignedUrlAsync(path, TimeSpan.FromMinutes(15));
    }
}
```

## 扩展点 / 自定义

- **自定义提供程序**：实现 `IFileStorageProvider`（建议继承 `FileStorageProviderBase` 复用计时/异常收敛/工具方法），用 `services.AddFileStorageProvider<TMyProvider>("MyStorage")` 注册（`TryAddSingleton` 具体类型 + 登记名称→类型），随后即可用 `GetProvider("MyStorage")` 取用，或在 `RouteProviderMappings` 中把某业务路由到它。
- **替换核心服务**：`IFileStorageProviderManager` / `IFileStorageRouter` 均以 `TryAddSingleton` 注册，可在模块中先行注册自定义实现以覆盖默认行为。
- **单后端便捷注册**：也可绕开配置驱动，直接调用 `AddLocalFileStorageProvider` / `AddMinioFileStorageProvider` / `AddAliyunOssFileStorageProvider` / `AddTencentCosFileStorageProvider`（各带可选 `Action<Options>` 配置委托）显式注册。

## 注意事项与最佳实践

- **上传不抛异常**：`UploadAsync` 内部把异常收敛为 `FileUploadResult { Success=false, ErrorMessage=... }`。调用方务必检查 `result.Success`，不要只 `try/catch`。
- **分片能力先检查**：调用分片方法前先看目标 Provider 的 `SupportChunkedUpload`；不支持的后端（如当前的 MinIO）会抛 `NotSupportedException`。
- **本地预签名无时效**：`LocalFileStorageProvider.GeneratePresignedUrlAsync` 只是返回静态直链，`expiresIn` 被忽略，也没有鉴权。需要真实时效/鉴权控制请改用云存储 Provider。
- **提供程序名称大小写**：Manager/Router 内部用大小写不敏感比较，但常量值有固定拼写（`MinIO`/`AliyunOSS`/`TencentCOS`），配置里建议直接用 `ObjectStorageProviderNames` 的常量拼写以免误配。
- **未注册即用会抛异常**：只有出现在 `EnabledProviders`/`DefaultProvider` 里（或经 `AddFileStorageProvider` 显式注册）的 Provider 才可被解析；`GetProvider` 未注册名称会抛 `InvalidOperationException`，需要静默判断请用 `TryGetProvider`。

## 依赖模块

- [XiHan.Framework.Core](./core) — 唯一的框架内部依赖，提供模块化与依赖注入基础。

第三方存储 SDK 为该包核心依赖：`Aliyun.OSS.SDK.NetCore`（阿里云 OSS）、`Minio`（MinIO）、`Tencent.QCloud.Cos.Sdk`（腾讯云 COS）。

## 相关模块

- [XiHan.Framework.VirtualFileSystem](./virtual-file-system) — 虚拟文件系统，面向本地物理目录与程序集嵌入资源的统一虚拟路径只读访问；不含云存储适配，与本包定位互补。
- [XiHan.Framework.Web.Api](./web-api) — 消费本地存储配置节挂载静态文件服务，实现上传文件的匿名直链访问。
