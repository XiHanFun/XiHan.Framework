# 对象存储

本章讲 5.0 的对象存储：容器与键、批量写入与删除、按键读取与范围读取、按前缀分页列举、分片上传、预签名地址与命名存储实例，以及文件系统与 S3 两个实现怎么选。适用于在应用里存取文件、附件与大对象的开发者。

完整 API 见 [ObjectStorage.Abstractions](../packages/objectstorage-abstractions)、[ObjectStorage.FileSystem](../packages/objectstorage-filesystem) 与 [ObjectStorage.S3](../packages/objectstorage-s3) 包页。

## 概念

对象存储只定义一个字节流级的契约 `IBlobStore`：容器里按键存放字节与元数据。写入与删除以批量为主，单条是批量的特例；读取按键，可以只读一段；列举按前缀分页。对象写入后整体替换，读取方要么看到旧对象，要么看到完整的新对象。容器由存储按需创建（文件系统）或由基础设施预先建好（S3 的桶），契约不管理容器。

子系统分成三个能力，各 Provider 提供其中一部分或全部：

| 能力 | 契约 | 文件系统（默认实现） | S3 |
| --- | --- | --- | --- |
| `objectstorage.blob@1` | `IBlobStore`：读写、删除、列举、范围读取 | 提供 | 提供 |
| `objectstorage.multipart@1` | `IBlobMultipartStore`：分片上传 | 提供（暂存目录） | 提供（原生 multipart） |
| `objectstorage.presign@1` | `IBlobUrlSigner`：预签名地址 | 由 `Hosting.AspNetCore` 的宿主代收发提供 | 提供（客户端直连存储） |

4.x 的虚拟文件系统在 5.0 不提供。读应用自带的只读文件（模板、静态数据）用 .NET 的 `IFileProvider`（`PhysicalFileProvider`、`ManifestEmbeddedFileProvider`）或程序集的嵌入资源；用户上传、需要按键存取的内容放对象存储。

## 启用

`Profiles.Full` 已装配文件系统存储；其他情况显式加入：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.ObjectStorage.FileSystem;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<FileSystemObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "FileSystem": { "RootPath": "/var/lib/app/blobs" } } } }
```

换成 S3 或兼容服务时改装 `S3ObjectStorageFeature`。它是显式 Provider，与文件系统同时出现时顶替文件系统，业务代码注入的仍是 `IBlobStore`：

```csharp
builder.AddXiHan(xihan => xihan.UseFullProfile().UseFeature<S3ObjectStorageFeature>());
```

```json
{
  "XiHan": {
    "ObjectStorage": {
      "S3": {
        "ServiceUrl": "http://localhost:9000",
        "Region": "us-east-1",
        "AccessKey": "dev-access-key",
        "SecretKey": "dev-secret-key",
        "ForcePathStyle": true,
        "HealthBucket": "uploads"
      }
    }
  }
}
```

## 标识

| 标识 | 规则 | 例子 |
| --- | --- | --- |
| 容器 `ContainerName` | `^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$`，与 S3 桶名兼容 | `invoices` |
| 键 `BlobKey` | `/` 分段，1 到 1024 个字符；每段非空、不是 `.` 或 `..`；不含空白、控制字符与 `\`；首段不是 `.xihan`（保留给框架）；区分大小写 | `2026/10/inv-1.pdf` |
| 自定义元数据名 | `^[a-z0-9][a-z0-9-]{0,63}$`，值为任意字符串 | `uploaded-by` |

`ContainerName` 与 `BlobKey` 都能从 `string` 隐式转换，不合法在构造时抛出 `ArgumentException`，不会进入 Provider。来自用户输入的键先用 `BlobKey.IsValid` 判断。

首段是 `.xihan` 的键（`BlobKey.ReservedSegment`，`.xihan` 本身与 `.xihan/…`，区分大小写）保留给框架自己的对象，例如 S3 的条件写入探测键 `.xihan/probe/conditional-write`：业务写不进、读不到、删不掉，各存储列举时跳过存储里已有的这类对象，所以它们不会混进业务列举（ADR-0090）。以 `.xihan` 开头的查询前缀不报错，列举结果总是空的。

## 读写

### 写入

单条写入用扩展方法 `PutAsync`，它转调批量方法。写入选项 `BlobPutOptions` 有内容类型（缺省 `application/octet-stream`）、自定义元数据与 `Overwrite`（缺省 `true`）。下面的端点把请求体存成一个对象，失败结果交给宿主写成 Problem Details：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

app.MapPut("/files/{name}", async (string name, HttpContext http, IBlobStore store, CancellationToken ct) =>
{
    var key = $"inbox/{name}";
    if (!BlobKey.IsValid(key))
    {
        return Results.BadRequest();
    }

    var options = new BlobPutOptions
    {
        ContentType = http.Request.ContentType ?? BlobPutOptions.DefaultContentType,
        // 不覆盖：同名对象已存在时返回 OBJECTSTORAGE_CONFLICT_001，宿主映射为 409
        Overwrite = false,
    };
    var put = await store.PutAsync("uploads", key, http.Request.Body, options, ct);
    if (!put.TryGetValue(out var metadata))
    {
        http.GetXiHanContext().ValueOrThrow().SetOutcome(put.Error!);
        return Results.Empty;
    }

    return Results.Ok(new { metadata.ETag, metadata.Length });
});
```

批量写入按顺序逐项执行，返回与入参同序的逐项结果；两项之间检查取消，已完成的项不回滚。空列表返回空，重复的键按顺序各执行一次：

```csharp
var results = await store.PutAsync("invoices",
[
    new BlobUpload("2026/10/inv-1.pdf", first, new BlobPutOptions { ContentType = "application/pdf" }),
    new BlobUpload("2026/10/inv-2.pdf", second, new BlobPutOptions { ContentType = "application/pdf" }),
], cancellationToken);

foreach (var result in results.Where(static r => r.Outcome.IsFailure))
{
    logger.LogWarning("Storing {Key} failed: {Code}", result.Key, result.Outcome.Error!.Code);
}
```

### 读取

`GetAsync` 打开内容流与元数据，返回的 `BlobContent` 要释放（释放时关闭流，S3 一并释放响应）。只要元数据时用 `HeadAsync`，只判断存在用 `ExistsAsync`，小对象一次读进内存用 `ReadAllBytesAsync`：

```csharp
app.MapGet("/files/{**key}", async (string key, HttpContext http, IBlobStore store, CancellationToken ct) =>
{
    if (!BlobKey.IsValid(key))
    {
        return Results.NotFound();
    }

    var opened = await store.GetAsync("uploads", key, ct);
    if (!opened.TryGetValue(out var blob))
    {
        http.GetXiHanContext().ValueOrThrow().SetOutcome(opened.Error!);
        return Results.Empty;
    }

    // 响应结束时释放 BlobContent，内容流随之关闭
    http.Response.RegisterForDisposeAsync(blob);
    return Results.Stream(blob.Content, blob.Metadata.ContentType);
});
```

元数据 `BlobMetadata` 有键、长度、内容类型、`ETag`（内容变化即变化）、最后修改时间与自定义元数据。不存在返回 `OBJECTSTORAGE_NOTFOUND_001`。

### 删除

`DeleteAsync` 批量删除，逐项结果的 `Outcome` 为 `true` 表示存在过并已删除，`false` 表示本来就不存在，失败时带错误。同一批里重复的键第二次报告不存在：

```csharp
var deleted = await store.DeleteAsync("invoices", ["2026/10/inv-1.pdf", "2026/10/inv-2.pdf"], cancellationToken);
var removed = deleted.Count(static d => d.Outcome.ValueOr(false));
```

单条删除用扩展方法 `DeleteAsync(container, key, cancellationToken)`。

### 列举

`ListAsync` 按键前缀列举，键的序数顺序，`PageSize` 为 1 到 1000（缺省 1000）；越界不在构造 `BlobQuery` 时抛出，列举返回 `OBJECTSTORAGE_VALIDATION_005`（HTTP 400），不访问存储——页大小常由请求绑定而来，越界是调用方可以改正的输入（ADR-0082）。续页标记 `ContinuationToken` 不透明，原样带回下一次查询：

```csharp
var query = new BlobQuery { Prefix = "2026/10/", PageSize = 100 };
while (true)
{
    var page = (await store.ListAsync("invoices", query, cancellationToken)).ValueOrThrow();
    foreach (var item in page.Items)
    {
        Console.WriteLine($"{item.Key} {item.Length}");
    }

    if (page.ContinuationToken is null)
    {
        break;
    }

    query = query with { ContinuationToken = page.ContinuationToken };
}
```

S3 的列举条目只有键、长度、`ETag` 与修改时间，内容类型为缺省值、自定义元数据为空，需要时再 `HeadAsync`。

## 范围读取

音视频拖动、断点续传下载只读一段：

```csharp
var opened = await store.GetAsync("videos", "raw/intro.mp4", new BlobRange(offset: 1_048_576, length: 65_536), cancellationToken);
if (opened.TryGetValue(out var part))
{
    await using (part)
    {
        // Range 是实际读到的范围；Metadata.Length 仍是对象总长
        var range = part.Range!;
        Console.WriteLine($"bytes {range.Offset}-{range.Last}/{range.TotalLength}");
    }
}
```

长度为空时读到末尾，超出末尾时截断。起点不在对象内（含空对象）返回 `OBJECTSTORAGE_VALIDATION_001`。

## 分片上传

大文件分片上传、断点续传用 `IBlobMultipartStore`（能力 `objectstorage.multipart@1`），限制与 S3 一致：

1. `StartAsync(container, key, options)` 发起上传，得到 `BlobMultipartUpload`（容器、键、上传标识、是否覆盖）。它可以持久化，跨请求、跨进程续传时用同样的四个值重建。
2. `UploadPartAsync(upload, partNumber, stream)` 上传编号 1 到 10000 的一片，可乱序、可重传（后一次覆盖），返回该片的 `BlobPart`（编号与 ETag）。
3. `ListPartsAsync(upload)` 按编号升序列出已上传的分片，续传时据此跳过已完成的片。
4. `CompleteAsync(upload, parts)` 用编号严格升序、ETag 与已上传一致的清单拼成对象；除最后一片外每片至少 5 MiB（`BlobMultipart.MinPartBytes`）。完成前对象不可见，完成是原子的。
5. `AbortAsync(upload)` 丢弃上传，上传不存在也成功。

| 情况 | 结果 |
| --- | --- |
| 清单为空、乱序、含未上传的片、ETag 不符或非末片过小 | `OBJECTSTORAGE_VALIDATION_002`，上传保留，可修正清单后再完成 |
| 上传不存在或已结束 | `OBJECTSTORAGE_NOTFOUND_002` |
| `Overwrite = false` 且对象已存在 | 发起与完成都返回 `OBJECTSTORAGE_CONFLICT_001` |

文件系统把分片暂存在容器下的 `uploads/<上传标识>/`，暂存的分片不计入配额，超过 `MultipartExpiryHours`（缺省 168 小时）仍未完成的上传在启动扫描时清掉；S3 直接映射原生 multipart。

## 预签名地址

`IBlobUrlSigner`（能力 `objectstorage.presign@1`）签出限时地址，客户端不带凭据直接下载或上传：

| 请求 | 方法 | 用途 |
| --- | --- | --- |
| `BlobUrlRequest.ForDownload(expiresIn, downloadFileName)` | `GET` | 下载，支持 `Range`；可带保存的文件名（`Content-Disposition`） |
| `BlobUrlRequest.ForUpload(expiresIn, contentType)` | `PUT` | 上传整个对象，可限定内容类型 |
| `BlobUrlRequest.ForPart(upload, partNumber, expiresIn)` | `PUT` | 上传分片上传的某一片，响应头 `ETag` 就是这一片的 ETag |

有效期缺省 30 分钟（`BlobUrlRequest.DefaultExpiresIn`），范围 1 秒到 7 天，超出时构造请求抛出 `ArgumentOutOfRangeException`。签出的 `BlobSignedUrl` 带地址、方法、失效时刻与 `Headers`：客户端必须原样发送这些请求头（例如限定的 `Content-Type`），否则验签不过。签名在本地计算，不检查对象是否存在。

```csharp
var link = (await signer.SignAsync("invoices", "2026/10/inv-1.pdf", BlobUrlRequest.ForDownload(downloadFileName: "发票.pdf"), cancellationToken)).ValueOrThrow();
// link.Url 交给前端；link.ExpiresAt 之后失效
```

S3 由存储自己签名（签名 V4），客户端直连存储。文件系统没有原生签名，由宿主代收发：装配 `HostedBlobUrlFeature` 并映射端点，签名是对方法、容器、键、过期时刻与各限定项的 HMAC-SHA256，无状态，持有同一密钥的多个实例互认。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Blobs;
using XiHan.Framework.ObjectStorage.FileSystem;

builder.AddXiHan(xihan => xihan
    .UseFeature<FileSystemObjectStorageFeature>()
    .UseFeature<HostedBlobUrlFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanBlobs();   // GET / PUT <BaseUrl>/{container}/{**key}
```

```json
{
  "XiHan": {
    "ObjectStorage": {
      "SignedUrls": {
        "BaseUrl": "/blobs",
        "Secret": "dev-signing-secret-at-least-32-bytes"
      }
    }
  }
}
```

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:ObjectStorage:SignedUrls:BaseUrl` | `/blobs` | 签出地址的前缀：以 `/` 开头的路径（相对应用的源）或绝对地址；端点的路由前缀取它的路径部分 |
| `XiHan:ObjectStorage:SignedUrls:Secret` | 无，必填 | 签名密钥，UTF-8 至少 32 字节；多实例部署必须一致；缺失或过短时装配抛出 `ArgumentException` |

宿主端点 `GET` 支持单个 `Range` 区间（206 与 `Content-Range`，不可满足回 416），`PUT` 写整个对象或一片并在响应头带 `ETag`。端点经内核管道，Observe 照常记录，不要求凭据，签名即授权；上传解除了请求体大小上限，单对象上限由存储把关。签名不符或过期回 403（`OBJECTSTORAGE_DENIED_001`）。没有装配 `HostedBlobUrlFeature` 就调用 `MapXiHanBlobs()`，调用处抛出 `FrameworkException`（`KERNEL_HOSTING_004`）。

::: warning 不要同时装 S3 与宿主代收发
`S3ObjectStorageFeature` 与 `HostedBlobUrlFeature` 都提供 `objectstorage.presign@1`，同时装配时要用 `XiHan:Providers:objectstorage.presign` 指定一个，否则启动失败。S3 自己能签名，用 S3 时不装 `HostedBlobUrlFeature`。
:::

### 大文件直传

服务端发起分片上传并签出各片地址，客户端按片直接传给存储（或宿主端点），最后由服务端完成。业务服务器不经手文件内容：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;

// 1. 发起：返回上传标识与每一片的签名地址
app.MapPost("/videos/{name}/uploads", async (string name, int parts, IBlobMultipartStore multipart, IBlobUrlSigner signer, CancellationToken ct) =>
{
    var upload = (await multipart.StartAsync("videos", $"raw/{name}", new BlobPutOptions { ContentType = "video/mp4" }, ct)).ValueOrThrow();
    var urls = new List<string>(parts);
    for (var number = 1; number <= parts; number++)
    {
        var signed = (await signer.SignAsync(upload.Container, upload.Key, BlobUrlRequest.ForPart(upload, number), ct)).ValueOrThrow();
        urls.Add(signed.Url.ToString());
    }

    return new UploadTicket(upload.UploadId, urls);
});

// 2. 客户端逐片 PUT 到签名地址，记下每个响应的 ETag

// 3. 完成：上传标识可持久化，这里按同样的容器与键重建
app.MapPost("/videos/{name}/uploads/{uploadId}/complete", async (string name, string uploadId, CompletedPart[] completed, IBlobMultipartStore multipart, CancellationToken ct) =>
{
    var upload = new BlobMultipartUpload("videos", $"raw/{name}", uploadId);
    var parts = completed.OrderBy(static p => p.Number).Select(static p => new BlobPart(p.Number, p.ETag)).ToList();
    var done = (await multipart.CompleteAsync(upload, parts, ct)).ValueOrThrow();
    return new { Key = done.Key.Value, done.Length, done.ETag };
});

public sealed record UploadTicket(string UploadId, IReadOnlyList<string> PartUrls);

public sealed record CompletedPart(int Number, string ETag);
```

用 Native AOT 发布时，`UploadTicket`、`CompletedPart` 要加进应用的 `JsonSerializerContext`，见[序列化与 Native AOT](./aot)。

## 命名存储实例

一个应用同时用多个存储（按租户或按用途各配一个桶，各有自己的端点与密钥）时，用注册表 `BlobStores` 管理命名实例。装配任一存储包就有这个单例，装配了的存储包都登记自己的命名实例 Provider，与它是否被选为缺省实例无关；缺省的 `IBlobStore` 注入方式不变，命名实例与缺省实例互不相干。

实例的定义 `BlobStoreDefinition` 是实例名（`^[a-z0-9][a-z0-9-]{0,63}$`）、Provider 名（`filesystem` 或 `s3`，不区分大小写）与设置。设置的键与该 Provider 的配置节相同、不区分大小写，未知的键是错误，拼错的键不会被静默忽略。定义有两个来源：

- 配置节 `XiHan:ObjectStorage:Stores:<name>`：`Provider` 加设置，首次取用时建出。
- 运行时登记：`GetOrAddAsync(definition)`，定义可以来自应用自己的库。

```json
{
  "XiHan": {
    "ObjectStorage": {
      "Stores": {
        "archive": { "Provider": "filesystem", "RootPath": "/var/lib/app/archive" },
        "media": { "Provider": "s3", "ServiceUrl": "http://localhost:9000", "AccessKey": "dev-access-key", "SecretKey": "dev-secret-key", "ForcePathStyle": "true", "HealthBucket": "media" }
      }
    }
  }
}
```

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Stores;

// 配置里定义的实例
var archive = (await stores.GetAsync("archive", cancellationToken)).ValueOrThrow();

// 来自应用库的存储配置：同名同定义复用同一实例，定义变了就建新实例并释放旧的
var definition = new BlobStoreDefinition("tenant-42", "s3", new Dictionary<string, string>
{
    ["ServiceUrl"] = tenant.StorageEndpoint,
    ["AccessKey"] = tenant.StorageAccessKey,
    ["SecretKey"] = tenant.StorageSecretKey,
});
var tenantStore = (await stores.GetOrAddAsync(definition, cancellationToken)).ValueOrThrow();
```

`tenant` 是应用自己的租户存储配置。注册表的语义：

| 操作 | 结果 |
| --- | --- |
| `GetAsync(name)` | 已建的直接返回；配置里有定义的首次取用时建出；都没有 → `OBJECTSTORAGE_NOTFOUND_003` |
| `GetOrAddAsync(definition)` | Provider 名与设置完全相同时返回已有实例；定义变了建新实例替换并释放旧的，替换时仍在旧实例上的调用可能失败 |
| Provider 未登记或设置不合法 | `OBJECTSTORAGE_VALIDATION_003`，已有的同名实例不受影响 |
| 后端此刻建不出来（目录无权限、磁盘错误） | `OBJECTSTORAGE_UNAVAILABLE_001`，已有的同名实例不受影响 |
| 建实例时的探测发现实例不能用（S3 探明服务不认条件写入、探测被拒绝或探测桶不存在） | Provider 给出的错误码（`OBJECTSTORAGE_UNAVAILABLE_003`、`OBJECTSTORAGE_DENIED_002`、`OBJECTSTORAGE_NOTFOUND_001`），不登记，下次取用重新探测，已有的同名实例不受影响；S3 只是暂时连不上时照常建出，后台重试 |
| `RemoveAsync(name)` | 移除并释放，返回是否存在过；配置里有定义的实例之后再取用会重新建出 |

文件系统的命名实例必须给 `RootPath`，否则会与缺省实例落在同一目录。S3 的命名实例各建各的客户端，不读连接绑定，建出时在实例的 `HealthBucket` 里探测服务是否认条件写入（见[ObjectStorage.S3](../packages/objectstorage-s3#条件写入探测)；探明不认、被拒绝、探测桶不存在都不建，只有连不上的照常建出、后台重试），实例被替换、移除或应用停止时停止重试、释放客户端。命名实例的能力随实例本身：S3 实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；文件系统实例实现分片，预签名经宿主签名器的 `ForStore(name)`，签出的地址带 `x-store=<name>`，实例名在签名里，改成别的实例或去掉都验签不过：

```csharp
var signer = app.Services.GetRequiredService<HostedBlobUrlSigner>().ForStore("archive");
var url = (await signer.SignAsync("files", "docs/a.txt", BlobUrlRequest.ForDownload(), cancellationToken)).ValueOrThrow().Url;
```

每个实例一条健康项 `objectstorage.store.<name>`，按 `XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds`（缺省 15 秒）在后台逐个探测，新实例建出后立即探一次；还没探过为 Started，探测失败为 Degraded，实例仍可取用。文件系统看总配额是否还有余量，S3 对实例的 `HealthBucket` 做 HEAD（没配时列桶），连通之后再看条件写入，尚未确认或不认时 Degraded。

::: warning 混装文件系统与 S3 时怎样指定缺省存储
缺省的 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 必须落在同一存储上，这条规则只约束被要求的契约：有特性在清单里依赖了这项能力，或 `XiHan:Providers` / `UseProvider` 指定了它的提供者。不指定时 S3 是显式 Provider，三项都归 S3，文件系统被顶替，但它的命名实例 Provider 照样登记（被顶替的存储包只登记命名实例，不建缺省实例、不启动）。

以文件系统为缺省实例、只用 S3 的命名实例时，指定对象存储一项就够：

```json
{
  "XiHan": {
    "Providers": {
      "objectstorage.blob": "xihan.objectstorage.filesystem"
    }
  }
}
```

这时 S3 的分片上传与预签名只是兜底选出（没人依赖、没人指定），S3 按被顶替处理，只登记命名实例；容器里没有缺省的 `IBlobMultipartStore` 与 `IBlobUrlSigner`，也不必装 `HostedBlobUrlFeature`。要用缺省的分片上传，再指定 `"objectstorage.multipart": "xihan.objectstorage.filesystem"`；要用缺省的预签名，装配 `HostedBlobUrlFeature` 并指定 `"objectstorage.presign": "xihan.hosting.blob-urls"`（它与 S3 都是显式 Provider，不指定本来就是 `KERNEL_CAPABILITY_004`）。

存储包被要求提供分片上传或预签名、却没被选为对象存储时，装配抛出 `OBJECTSTORAGE_VALIDATION_004`，应用启动失败，消息列出被要求的能力与要改的键：比如只指定了对象存储，而应用的某个特性依赖 `objectstorage.presign`，预签名就解析到 S3 且是被要求的。选择规则见[能力包与清单](./modularity)。
:::

## 两个实现

| | 文件系统 | S3 |
| --- | --- | --- |
| 清单角色 | `role: default`，`Profiles.Full` 装配 | `role: provider` |
| 布局 | `<RootPath>/<container>/data/<key>` 存内容，`meta/<key>.json` 存元数据 | 容器即桶，键即对象键 |
| 原子性 | 先写同目录临时文件再原子替换；不覆盖写入以不替换的移动原子占位 | 服务端保证；不覆盖写入带 `If-None-Match: *`，服务是否认它先探明（启动时探一次，没探明就后台重试，探明之前不覆盖写入返回 `OBJECTSTORAGE_UNAVAILABLE_004`） |
| `ETag` | 内容的 SHA-256 | 服务端给出 |
| 配额 | `MaxTotalBytes`（缺省 1 GiB）、`MaxBlobBytes`（缺省 256 MiB），超限 `OBJECTSTORAGE_UNAVAILABLE_002` | 无，由桶策略管理 |
| 健康项 | `objectstorage.filesystem.capacity`：用量达到总配额为 Degraded | `objectstorage.s3.connection`：按 `HealthProbeIntervalSeconds`（缺省 15 秒）重探连通性，并入条件写入的状态（连不上 Degraded；不认，以及启动之后才被拒绝或缺桶 Failed） |
| 预签名 | 宿主代收发 | 原生，客户端直连 |
| AOT | 兼容 | 不兼容（AWS SDK 基于运行时反射） |

不覆盖写入在两个实现上都是原子的：多个写入方同时以 `Overwrite = false` 写同一个新键，只有一个成功，其余 `OBJECTSTORAGE_CONFLICT_001`（一致性套件核对）。S3 靠服务端的条件写入：不认条件写入的兼容服务、被拒的凭据、不存在的探测桶在启动时即启动失败（配置问题要早暴露）；只有启动时连不上照常启动，探明之前不覆盖写入返回 `OBJECTSTORAGE_UNAVAILABLE_004`；之后才探明不认的不覆盖写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`（ADR-0089、ADR-0090、ADR-0093）。

文件系统存储单进程正确：启动时扫描目录得到已用量，之后在进程内计量，多个进程共享同一目录时配额只是近似。S3 Provider 覆盖 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等），`ServiceUrl` 为空时按 `Region` 访问 Amazon S3；各兼容服务对路径式与虚拟主机式寻址的支持不同，按服务商的说明设置 `ForcePathStyle`。阿里云 OSS、腾讯云 COS、Cloudflare R2 也只用这一个包，不为各家的私有禁止覆盖头做适配：它们是否认标准的 `If-None-Match: *` 框架没有实测，接入前要实测（缺省的条件写入探测即实测，或照 [ObjectStorage.S3](../packages/objectstorage-s3#云厂商的兼容服务) 用 curl 发两次条件写入），不认就关 `ConditionalWrites`——关掉后这个存储上 `Overwrite = false` 的写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`，"只在不存在时写入"的业务要改用不冲突的键或覆盖写入（ADR-0090）。两个实现的细节见各自包页。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `OBJECTSTORAGE_NOTFOUND_001` | 对象不存在 |
| `OBJECTSTORAGE_NOTFOUND_002` | 分片上传不存在或已结束 |
| `OBJECTSTORAGE_NOTFOUND_003` | 命名存储实例不存在 |
| `OBJECTSTORAGE_VALIDATION_001` | 读取范围的起点不在对象内 |
| `OBJECTSTORAGE_VALIDATION_002` | 完成分片上传时的分片清单不合法 |
| `OBJECTSTORAGE_VALIDATION_003` | 命名存储实例的定义不合法（Provider 未登记或设置不合法） |
| `OBJECTSTORAGE_VALIDATION_004` | 缺省存储的能力选择不一致：被要求的分片上传或预签名与对象存储不在同一存储（装配时抛出，启动失败） |
| `OBJECTSTORAGE_VALIDATION_005` | 列举的页大小不在 1 到 1000 之间（HTTP 400） |
| `OBJECTSTORAGE_DENIED_001` | 预签名地址无效或已过期 |
| `OBJECTSTORAGE_DENIED_002` | 条件写入探测被存储拒绝（S3：凭据无效、只读或无权写探测键；启动或建实例时即失败，启动之后才被拒时健康项 Failed、后台照常重试，已定，ADR-0097） |
| `OBJECTSTORAGE_CONFLICT_001` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | 对象存储不可达或 IO 失败 |
| `OBJECTSTORAGE_UNAVAILABLE_002` | 配额或单对象大小超限 |
| `OBJECTSTORAGE_UNAVAILABLE_003` | 不覆盖写入在本存储上无法保证：S3 兼容服务不认条件写入（启动时探明即启动失败或不建命名实例，之后探明的不覆盖写入一律返回它），或存储关闭了条件写入（`ConditionalWrites = false`） |
| `OBJECTSTORAGE_UNAVAILABLE_004` | 不覆盖写入暂不可用：S3 兼容服务是否认条件写入尚未确认（启动时连不上，后台在重试；之后的探测被拒绝或缺桶时同样返回它；HTTP 503，探测通过后自动恢复） |

HTTP 宿主按错误码的类别段映射状态码，见[结果与错误](./errors)。标识不合法属于编程错误，构造时抛出 `ArgumentException`。

## 相关

- [配置与选项](./configuration)
- [能力包与清单](./modularity)
- [宿主与 Web 应用](./web)
- [ObjectStorage.Abstractions](../packages/objectstorage-abstractions)
- [ObjectStorage.FileSystem](../packages/objectstorage-filesystem)
- [ObjectStorage.S3](../packages/objectstorage-s3)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050
