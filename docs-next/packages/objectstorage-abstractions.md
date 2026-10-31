# XiHan.Framework.ObjectStorage.Abstractions

对象存储的抽象包：容器、键与元数据，`IBlobStore`（批量写入与删除、按键读取、范围读取、按前缀分页列举），可选的分片上传 `IBlobMultipartStore` 与预签名 `IBlobUrlSigner`，以及命名存储实例的注册表 `BlobStores`。业务代码只依赖本包，存储后端由 Provider 决定。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.ObjectStorage.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `objectstorage.blob@1`、`objectstorage.multipart@1`、`objectstorage.presign@1`，由 [ObjectStorage.FileSystem](./objectstorage-filesystem)（默认实现）、[ObjectStorage.S3](./objectstorage-s3) 与 [Hosting.AspNetCore](./hosting-aspnetcore) 提供 |
| 特性入口 | — |
| 配置节 | `XiHan:ObjectStorage:Stores`、`XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions` 10.0.13、`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`IBlobStore` 是字节流级的契约，Provider 实现六个操作：批量写入、按键读取、范围读取、只读元数据、批量删除与按前缀列举。批量操作按顺序逐项执行、逐项返回结果，两项之间检查取消，已完成的项不回滚；单条写法的扩展方法在 `BlobStoreExtensions` 里，都转调批量方法。写入是原子的，读取方要么看到旧对象，要么看到完整的新对象。

标识在构造时校验：容器名 `ContainerName` 兼容 S3 桶名，键 `BlobKey` 是 `/` 分段的路径，不合法抛出 `ArgumentException`，不会进入 Provider。基础设施失败（IO、网络）以 `OBJECTSTORAGE_UNAVAILABLE_001` 的失败结果返回，不抛出。

分片上传与预签名是两个可选能力，接口独立：存储没有原生预签名时，宿主可以代为收发（`Hosting.AspNetCore` 的 `HostedBlobUrlFeature`）。命名存储实例（ADR-0050）让一个应用同时用多个存储：每个存储包登记一个 `IBlobStoreProvider`，注册表 `BlobStores` 按定义建实例、按名缓存、定义变了就替换。

## 何时使用

业务代码注入 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 或 `BlobStores` 时引用本包；具体后端装 [ObjectStorage.FileSystem](./objectstorage-filesystem) 或 [ObjectStorage.S3](./objectstorage-s3)。实现新的存储后端时实现本包的接口，并通过 `ObjectStorage.Conformance`。用法讲解见[对象存储](../guide/storage)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.ObjectStorage.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已引用本包与文件系统实现。本包没有特性，装配任一存储包后即可注入上述接口。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:ObjectStorage:Stores:<name>:Provider` | `string` | — | 命名实例的 Provider 名：`filesystem` 或 `s3`，不区分大小写 |
| `XiHan:ObjectStorage:Stores:<name>:<设置>` | `string` | — | 该 Provider 的设置，键同其配置节；未知键报 `OBJECTSTORAGE_VALIDATION_003` |
| `XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds` | 正数 | 15 | 命名实例健康探测的间隔，也是单次探测的超时 |

```json
{
  "XiHan": {
    "ObjectStorage": {
      "StoreHealthProbeIntervalSeconds": 30,
      "Stores": {
        "archive": { "Provider": "filesystem", "RootPath": "/var/lib/app/archive" }
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Keys.ContainerName` | 容器名，`^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$`；`IsValid`、`Validate`，可从 `string` 隐式转换 |
| `Keys.BlobKey` | 键：`/` 分段，1 到 1024 个字符，每段非空且不是 `.` / `..`，不含空白、控制字符与 `\`，区分大小写；`IsValid`、`IsValidPrefix`、`Validate`，可从 `string` 隐式转换 |
| `Blobs.IBlobStore` | `PutAsync(container, uploads)`、`GetAsync(container, key)`、`GetAsync(container, key, range)`、`HeadAsync`、`DeleteAsync(container, keys)`、`ListAsync(container, query)` |
| `Blobs.BlobStoreExtensions` | `PutAsync(container, key, stream 或 bytes, options)`、`DeleteAsync(container, key)`、`ExistsAsync`、`ReadAllBytesAsync` |
| `Blobs.BlobUpload` | 一项写入：键、内容流、选项 |
| `Blobs.BlobPutOptions` | `ContentType`（缺省 `application/octet-stream`）、`Metadata`（名 `^[a-z0-9][a-z0-9-]{0,63}$`）、`Overwrite`（缺省 `true`）；`Default` |
| `Blobs.BlobPutResult` / `BlobDeleteResult` | 逐项结果：写入为元数据或错误；删除为是否存在过或错误 |
| `Blobs.BlobMetadata` | 键、长度、内容类型、`ETag`、最后修改时间、自定义元数据 |
| `Blobs.BlobContent` | 元数据、内容流与可选的实际范围 `Range`；`IAsyncDisposable`，释放时关闭流 |
| `Blobs.BlobRange` / `BlobContentRange` | 范围请求（起点与可选长度）与实际范围（起点、长度、总长、`Last`） |
| `Listing.BlobQuery` / `BlobPage` | 列举条件（`Prefix`、`PageSize` 1–1000 缺省 1000、`ContinuationToken`）与一页结果 |
| `Multipart.IBlobMultipartStore` | `StartAsync`、`UploadPartAsync`、`ListPartsAsync`、`CompleteAsync`、`AbortAsync` |
| `Multipart.BlobMultipartUpload` | 一次分片上传：容器、键、上传标识、是否覆盖；可持久化后续传 |
| `Multipart.BlobPart` / `BlobMultipart` | 分片（编号、ETag、长度）；常量 `MaxPartNumber` = 10000、`MinPartBytes` = 5 MiB |
| `Signing.IBlobUrlSigner` | `SignAsync(container, key, request)` → `BlobSignedUrl` |
| `Signing.BlobUrlRequest` | `ForDownload`、`ForUpload`、`ForPart`；`DefaultExpiresIn` = 30 分钟，`MaxExpiresIn` = 7 天 |
| `Signing.BlobSignedUrl` | 地址、方法、失效时刻与客户端必须发送的请求头 |
| `Signing.BlobContentDisposition` | `Attachment(fileName)`：下载文件名的统一写法，非 ASCII 文件名带 `filename*` |
| `Stores.BlobStores` | 命名实例注册表：`GetAsync(name)`、`GetOrAddAsync(definition)`、`RemoveAsync(name)`、`Names`、`Providers`；同时是健康项贡献者 |
| `Stores.BlobStoreDefinition` | 实例名（`^[a-z0-9][a-z0-9-]{0,63}$`）、Provider 名与设置；`Describes(other)` 判断定义是否相同 |
| `Stores.IBlobStoreProvider` | 存储包的命名实例工厂：`Name`、`CreateAsync(definition)`、`ProbeAsync(store)` |
| `Stores.BlobStoreServiceCollectionExtensions` | `AddBlobStoreProvider<TProvider>(configuration)`：存储包装配时登记 Provider，并确保注册表只登记一次 |
| `ObjectStorageCapabilities` / `ObjectStorageErrors` / `ObjectStorageMessages` | 能力 ID、错误码与消息键常量，`ObjectStorageMessages.Catalog` 是本包的消息目录 |

## 使用示例

写入、读取、列举与删除：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;

var put = await store.PutAsync("invoices", "2026/10/inv-1.pdf", stream, new BlobPutOptions { ContentType = "application/pdf" }, ct);
var page = await store.ListAsync("invoices", new BlobQuery { Prefix = "2026/10/", PageSize = 100 }, ct);
var deleted = await store.DeleteAsync("invoices", ["2026/10/inv-1.pdf", "2026/10/inv-2.pdf"], ct);   // 逐项：存在过 / 不存在 / 失败

await using var content = (await store.GetAsync("invoices", "2026/10/inv-1.pdf", ct)).ValueOrThrow();
await content.Content.CopyToAsync(destination, ct);
```

断点续传与限时下载链接：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;

// 服务端发起，客户端按片直传，服务端完成
var upload = (await multipart.StartAsync("videos", key, new BlobPutOptions { ContentType = "video/mp4" }, ct)).ValueOrThrow();
var partUrl = (await signer.SignAsync("videos", key, BlobUrlRequest.ForPart(upload, 1), ct)).ValueOrThrow();
var done = await multipart.CompleteAsync(upload, parts, ct);

// 限时下载链接（缺省 30 分钟）
var link = (await signer.SignAsync("invoices", key, BlobUrlRequest.ForDownload(downloadFileName: "发票.pdf"), ct)).ValueOrThrow().Url;
```

实现自己的存储后端时，用一致性套件验证；测试项目引用 `XiHan.Framework.ObjectStorage.Conformance`：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Conformance;

namespace Storage.Tests;

public sealed class AzureBlobStoreConformanceTests : BlobStoreConformance
{
    // 返回被测的存储；实现了 IBlobMultipartStore 时分片用例一并执行，否则跳过
    protected override IBlobStore CreateStore() => new AzureBlobStore(TestSettings.ConnectionString);
}
```

`AzureBlobStore` 与 `TestSettings` 代表应用自己的实现与测试设置，5.0 没有提供 Azure Blob Provider。命名实例的 Provider 另继承 `BlobStoreProviderConformance`，实现 `CreateProvider()`、`ValidSettings()` 与 `OtherValidSettings()`。

## 行为与限制

- 写入：已存在且 `Overwrite = false` 的项返回 `OBJECTSTORAGE_CONFLICT_001`，不写入；配额或单对象大小超限返回 `OBJECTSTORAGE_UNAVAILABLE_002`，不写入。空列表返回空，重复的键按顺序各执行一次。
- 删除：不存在的项成功并报告为不存在；同一批里重复的键第二次报告不存在。
- 列举：键的序数顺序；续页标记不透明，原样带回。
- 范围读取：长度为空读到末尾，超出末尾截断；起点不在对象内（含空对象）返回 `OBJECTSTORAGE_VALIDATION_001`；`BlobMetadata.Length` 仍是总长。
- 分片上传：编号 1–10000，可乱序、可重传；完成时清单编号严格升序、ETag 相符、非末片至少 5 MiB；完成前对象不可见；放弃是幂等的。
- 预签名：有效期 1 秒到 7 天，签名在本地计算，不检查对象是否存在。
- 命名实例：同名同定义复用；定义变了替换并释放旧实例，替换时仍在旧实例上的调用可能失败；Provider 未登记或设置不合法、后端建不出来时，已有的同名实例不受影响。每个实例一条健康项 `objectstorage.store.<name>`，还没探过为 Started，探测失败为 Degraded。

| 错误码 | 常量 | 含义 |
| --- | --- | --- |
| `OBJECTSTORAGE_NOTFOUND_001` | `BlobNotFound` | 对象不存在 |
| `OBJECTSTORAGE_NOTFOUND_002` | `UploadNotFound` | 分片上传不存在或已结束 |
| `OBJECTSTORAGE_NOTFOUND_003` | `StoreNotFound` | 命名存储实例不存在 |
| `OBJECTSTORAGE_VALIDATION_001` | `RangeNotSatisfiable` | 读取范围的起点不在对象内 |
| `OBJECTSTORAGE_VALIDATION_002` | `PartsInvalid` | 分片清单不合法 |
| `OBJECTSTORAGE_VALIDATION_003` | `StoreDefinitionInvalid` | 命名实例的定义不合法 |
| `OBJECTSTORAGE_DENIED_001` | `SignatureInvalid` | 预签名地址无效或已过期 |
| `OBJECTSTORAGE_CONFLICT_001` | `BlobExists` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | `StoreUnavailable` | 对象存储不可达或 IO 失败 |
| `OBJECTSTORAGE_UNAVAILABLE_002` | `QuotaExceeded` | 配额或单对象大小超限 |

## 相关

- [对象存储](../guide/storage)
- [ObjectStorage.FileSystem](./objectstorage-filesystem)
- [ObjectStorage.S3](./objectstorage-s3)
- [Hosting.AspNetCore](./hosting-aspnetcore)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050
