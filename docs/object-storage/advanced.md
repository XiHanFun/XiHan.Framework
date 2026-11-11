# 分片、预签名与命名实例

这一页讲对象存储的进阶能力：只读一段的范围读取、大文件的分片上传与断点续传、让客户端不带凭据直接上传下载的预签名地址（S3 原生签名与宿主代收发）、服务端只发票不经手内容的大文件直传，以及一个应用同时用多个存储时的命名存储实例。读完后，我们能做出视频拖动、断点续传和按租户分桶的存储方案。前置知识是[读写与列举](/object-storage/basics)与[ASP.NET Core 宿主](/web/hosting)。

## 为什么需要

几 GB 的视频一次 `PUT` 上传，网络一抖就得从头再来；所有下载都经业务服务器转发，带宽与线程被文件占满；多租户系统每个租户要用自己的桶和密钥。这三个问题分别由分片上传、预签名地址与命名存储实例解决：分片可乱序可重传、完成才可见；预签名把读写直接交给存储（S3）或宿主端点（文件系统），签名即授权、限时失效；命名实例在同一个进程里按名字管理多个存储，定义可以来自配置或应用的库。

## 概念

| 能力 | 契约 | 文件系统 | S3 |
| --- | --- | --- | --- |
| `objectstorage.multipart@1` | `IBlobMultipartStore` | 分片暂存在容器下的 `uploads/<上传标识>/` | 原生 multipart |
| `objectstorage.presign@1` | `IBlobUrlSigner` | 没有原生签名，由 `Hosting.AspNetCore` 的 `HostedBlobUrlFeature` 代收发 | 签名 V4，客户端直连存储 |
| 命名实例 | `BlobStores` 注册表、`BlobStoreDefinition` | Provider 名 `filesystem` | Provider 名 `s3` |

| 类型 | 说明 |
| --- | --- |
| `BlobRange(offset, length)` | 读取范围；`BlobContent.Range` 是实际读到的 `BlobContentRange`（`Offset`、`Length`、`Last`、`TotalLength`） |
| `BlobMultipartUpload` | 一次分片上传：容器、键、上传标识 `UploadId`、是否覆盖；可持久化、按同样的值重建 |
| `BlobPart` | 一片：编号与 ETag |
| `BlobUrlRequest` / `BlobSignedUrl` | 签名请求（方法、有效期、限定项）与签出的地址（`Url`、`Method`、`ExpiresAt`、`Headers`） |

## 最小示例

文件系统存储加宿主代收发，签出一个 30 分钟内有效的下载地址，前端拿地址直接下载，不带凭据：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.FileSystem;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseFeature<FileSystemObjectStorageFeature>()
    .UseFeature<HostedBlobUrlFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanBlobs();   // GET / PUT <BaseUrl>/{container}/{**key}

app.MapGet("/invoices/{id}/link", async (string id, IBlobUrlSigner signer, CancellationToken ct) =>
{
    var link = (await signer.SignAsync(new ContainerName("invoices"), new BlobKey($"2026/10/{id}.pdf"), BlobUrlRequest.ForDownload(expiresIn: null, downloadFileName: "发票.pdf"), ct)).ValueOrThrow();
    // link.Url 交给前端；link.ExpiresAt 之后失效
    return new { Url = link.Url.ToString(), link.ExpiresAt };
});

app.Run();
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

运行后：`GET /invoices/inv-1/link` 返回形如 `/blobs/invoices/2026/10/inv-1.pdf?…` 的地址与失效时刻；浏览器打开它得到 PDF 与 `Content-Disposition: attachment; filename*=…发票.pdf`；改动地址里任何一个字符或过期后再访问得到 403（`OBJECTSTORAGE_DENIED_001`）。换成 `S3ObjectStorageFeature` 时去掉 `HostedBlobUrlFeature` 与 `MapXiHanBlobs()`，同一段签名代码签出的是直连存储的 S3 地址。

## 范围读取

音视频拖动、断点续传下载只读一段：

```csharp
var opened = await store.GetAsync(new ContainerName("videos"), new BlobKey("raw/intro.mp4"), new BlobRange(offset: 1_048_576, length: 65_536), cancellationToken);
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

1. `StartAsync(container, key, options)` 发起上传，得到 `BlobMultipartUpload`（容器、键、上传标识、是否覆盖）。它可以持久化，跨请求、跨进程续传时用同样的值重建。
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

S3 由存储自己签名（签名 V4），客户端直连存储。文件系统没有原生签名，由宿主代收发：装配 `HostedBlobUrlFeature` 并映射端点（见最小示例），签名是对方法、容器、键、过期时刻与各限定项的 HMAC-SHA256，无状态，持有同一密钥的多个实例互认。

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `XiHan:ObjectStorage:SignedUrls:BaseUrl` | `/blobs` | 签出地址的前缀：以 `/` 开头的路径（相对应用的源）或绝对地址；端点的路由前缀取它的路径部分 |
| `XiHan:ObjectStorage:SignedUrls:Secret` | 无，必填 | 签名密钥，UTF-8 至少 32 字节；多实例部署必须一致；缺失或过短时装配抛出 `ArgumentException` |

宿主端点 `GET` 支持单个 `Range` 区间（206 与 `Content-Range`，不可满足回 416），`PUT` 写整个对象或一片并在响应头带 `ETag`。端点经内核管道，Observe 照常记录，不要求凭据，签名即授权；上传解除了请求体大小上限，单对象上限由存储把关。签名不符或过期回 403（`OBJECTSTORAGE_DENIED_001`）。没有装配 `HostedBlobUrlFeature` 就调用 `MapXiHanBlobs()`，调用处抛出 `FrameworkException`（`KERNEL_HOSTING_004`）。

端点的响应都带 `X-Content-Type-Options: nosniff`。写出内容时还带沙箱的 `Content-Security-Policy`：用户上传的 HTML、SVG 在这里跑不了脚本，图片、视频和 PDF 照常预览。把这个地址上的 HTML 当网页用的应用，在 `UseXiHan` 之前的中间件里设自己的 `Content-Security-Policy`，端点不覆盖。S3 直传直下的地址不经宿主，不受这一条影响。

::: warning 不要同时装 S3 与宿主代收发
`S3ObjectStorageFeature` 与 `HostedBlobUrlFeature` 都提供 `objectstorage.presign@1`，同时装配时要用 `XiHan:Providers:objectstorage.presign` 指定一个，否则启动失败。S3 自己能签名，用 S3 时不装 `HostedBlobUrlFeature`。
:::

## 大文件直传

服务端发起分片上传并签出各片地址，客户端按片直接传给存储（或宿主端点），最后由服务端完成。业务服务器不经手文件内容：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;

// 1. 发起：返回上传标识与每一片的签名地址
app.MapPost("/videos/{name}/uploads", async (string name, int parts, IBlobMultipartStore multipart, IBlobUrlSigner signer, CancellationToken ct) =>
{
    var upload = (await multipart.StartAsync(new ContainerName("videos"), new BlobKey($"raw/{name}"), new BlobPutOptions { ContentType = "video/mp4" }, ct)).ValueOrThrow();
    var urls = new List<string>(parts);
    for (var number = 1; number <= parts; number++)
    {
        var signed = (await signer.SignAsync(upload.Container, upload.Key, BlobUrlRequest.ForPart(upload, number), ct)).ValueOrThrow();
        urls.Add(signed.Url.ToString());
    }

    return new UploadTicket(upload.UploadId, urls);
});

// 2. 客户端逐片 PUT 到签名地址，记下每个响应的 ETag

// 3. 完成：上传标识可持久化，这里按同样的容器、键与覆盖标志重建（覆盖标志必填，要与开始时一致）
app.MapPost("/videos/{name}/uploads/{uploadId}/complete", async (string name, string uploadId, CompletedPart[] completed, IBlobMultipartStore multipart, CancellationToken ct) =>
{
    var upload = new BlobMultipartUpload(new ContainerName("videos"), new BlobKey($"raw/{name}"), uploadId) { Overwrite = true };
    var parts = completed.OrderBy(static p => p.Number).Select(static p => new BlobPart(p.Number, p.ETag)).ToList();
    var done = (await multipart.CompleteAsync(upload, parts, ct)).ValueOrThrow();
    return new { Key = done.Key.Value, done.Length, done.ETag };
});

public sealed record UploadTicket(string UploadId, IReadOnlyList<string> PartUrls);

public sealed record CompletedPart(int Number, string ETag);
```

用 Native AOT 发布时，`UploadTicket`、`CompletedPart` 要加进应用的 `JsonSerializerContext`，见[JSON 序列化与 Native AOT](/common/serialization)。

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

`stores` 是注入的 `BlobStores`，`tenant` 是应用自己的租户存储配置。注册表的语义：

| 操作 | 结果 |
| --- | --- |
| `GetAsync(name)` | 已建的直接返回；配置里有定义的首次取用时建出；都没有 → `OBJECTSTORAGE_NOTFOUND_003` |
| `GetOrAddAsync(definition)` | Provider 名与设置完全相同时返回已有实例；定义变了建新实例替换并释放旧的，替换时仍在旧实例上的调用可能失败 |
| Provider 未登记或设置不合法 | `OBJECTSTORAGE_VALIDATION_003`，已有的同名实例不受影响 |
| 后端此刻建不出来（目录无权限、磁盘错误） | `OBJECTSTORAGE_UNAVAILABLE_001`，已有的同名实例不受影响 |
| 建实例时的探测发现实例不能用（S3 探明服务不认条件写入、探测被拒绝或探测桶不存在） | Provider 给出的错误码（`OBJECTSTORAGE_UNAVAILABLE_003`、`OBJECTSTORAGE_DENIED_002`、`OBJECTSTORAGE_NOTFOUND_001`），不登记，下次取用重新探测，已有的同名实例不受影响；S3 只是暂时连不上时照常建出，后台重试 |
| `RemoveAsync(name)` | 移除并释放，返回是否存在过；配置里有定义的实例之后再取用会重新建出 |

文件系统的命名实例必须给 `RootPath`，否则会与缺省实例落在同一目录。S3 的命名实例各建各的客户端，不读连接绑定，建出时在实例的 `HealthBucket` 里探测服务是否认条件写入，实例被替换、移除或应用停止时停止重试、释放客户端。命名实例的能力随实例本身：S3 实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；文件系统实例实现分片，预签名经宿主签名器的 `ForStore(name)`，签出的地址带 `x-store=<name>`，实例名在签名里，改成别的实例或去掉都验签不过：

```csharp
var signer = app.Services.GetRequiredService<HostedBlobUrlSigner>().ForStore("archive");
var url = (await signer.SignAsync(new ContainerName("files"), new BlobKey("docs/a.txt"), BlobUrlRequest.ForDownload(), cancellationToken)).ValueOrThrow().Url;
```

每个实例一条健康项 `objectstorage.store.<name>`，按 `XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds`（缺省 15 秒）在后台逐个探测，新实例建出后立即探一次；还没探过为 Started，探测失败为 Degraded，实例仍可取用。文件系统看总配额是否还有余量，S3 对实例的 `HealthBucket` 做 HEAD（没配时列桶），连通之后再看条件写入，尚未确认或不认时 Degraded。健康项的汇总见[生命周期与健康](/startup/lifecycle)。

S3 命名实例的定义可能来自应用的库，端点可能是租户填的，所以它的客户端过[出站地址策略](/security/outbound)，缺省只能连公网地址。自建的 MinIO 等要放行，比如把主机名写进 `XiHan:Security:Outbound:AllowedHosts`。被拒绝与连不上同样处理：建实例时的探测只算尚未确认，读写返回 `OBJECTSTORAGE_UNAVAILABLE_001`，日志里有一条 Warning。缺省实例的端点是运维配的，不过策略。

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

存储包被要求提供分片上传或预签名、却没被选为对象存储时，装配抛出 `OBJECTSTORAGE_VALIDATION_004`，应用启动失败，消息列出被要求的能力与要改的键。选择规则见[能力图谱与 Provider](/startup/capabilities)。
:::

## 常见问题

### 分片要多大，可以不按顺序传吗

除最后一片外每片至少 5 MiB，编号 1 到 10000，可乱序、可重传，后一次覆盖前一次。完成时清单必须按编号严格升序、ETag 与已上传一致，否则 `OBJECTSTORAGE_VALIDATION_002`，上传保留，修正清单再完成。

### 浏览器刷新了，分片上传怎么续

把 `BlobMultipartUpload` 的容器、键、上传标识存进业务表；续传时用同样的值重建，`ListPartsAsync` 列出已上传的片，跳过它们继续传。未完成的上传在文件系统上保留 `MultipartExpiryHours`，S3 上按桶的生命周期规则。

### 预签名地址泄露了怎么办

地址在 `ExpiresAt` 之后自动失效，有效期最长 7 天，按业务取最短可用值（下载几分钟、上传按文件大小估）。宿主代收发时轮换 `Secret` 即让全部已签地址失效；S3 的地址随凭据轮换失效。

### 用 S3 还要不要 `HostedBlobUrlFeature`

不要。S3 自己签名、客户端直连存储；两者都提供 `objectstorage.presign@1`，同时装配要显式指定一个，否则启动失败。

### 每个租户一个桶，怎样切换

用命名实例：租户的端点与密钥存在应用的库里，取用时构造 `BlobStoreDefinition` 调 `GetOrAddAsync`，同名同定义复用、定义变了自动换新。注意租户填的端点经出站地址策略，内网 MinIO 要先放行。

## 相关包与下一步

- [ObjectStorage.Abstractions](/packages/objectstorage-abstractions)：分片、签名与命名实例的契约
- [ObjectStorage.FileSystem](/packages/objectstorage-filesystem)、[ObjectStorage.S3](/packages/objectstorage-s3)：两个实现的配置与行为
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)：`HostedBlobUrlFeature` 与 `MapXiHanBlobs`
- 下一章：[全球化和本地化](/common/localization)
