# 读写与列举

这一页讲对象存储的基本用法：容器与键的文法、怎样启用文件系统或 S3 实现、怎样写入、读取、删除和按前缀分页列举对象，两个实现的差异与错误码。读完后，我们能在应用里把上传的文件存成对象并按键取回。前置知识是[特性与清单](/startup/features)、[端点里使用框架](/web/endpoints)与[结果与错误处理](/web/errors)。

## 为什么需要

用户上传的附件、生成的报表、导出的文件都要有地方放。直接写本地磁盘在多实例部署时各节点看不到彼此的文件，直接用某家云 SDK 又把业务绑死在一家服务上，开发环境还得先有账号。对象存储子系统只定义一个字节流级的契约 `IBlobStore`：开发与单机用文件系统实现，上线换 S3 或兼容服务，业务代码不变；写入与删除以批量为主，读取支持范围，列举按前缀分页，错误以 `Result` 返回而不是抛异常。

## 概念

`IBlobStore` 在容器里按键存放字节与元数据。对象写入后整体替换，读取方要么看到旧对象，要么看到完整的新对象。容器由存储按需创建（文件系统）或由基础设施预先建好（S3 的桶），契约不管理容器。

子系统分成三个能力，各 Provider 提供其中一部分或全部：

| 能力 | 契约 | 文件系统（默认实现） | S3 |
| --- | --- | --- | --- |
| `objectstorage.blob@1` | `IBlobStore`：读写、删除、列举、范围读取 | 提供 | 提供 |
| `objectstorage.multipart@1` | `IBlobMultipartStore`：分片上传 | 提供（暂存目录） | 提供（原生 multipart） |
| `objectstorage.presign@1` | `IBlobUrlSigner`：预签名地址 | 由 `Hosting.AspNetCore` 的宿主代收发提供 | 提供（客户端直连存储） |

这一页讲第一个能力；后两个见[分片、预签名与命名实例](/object-storage/advanced)。

框架不提供虚拟文件系统。读应用自带的只读文件（模板、静态数据）用 .NET 的 `IFileProvider`（`PhysicalFileProvider`、`ManifestEmbeddedFileProvider`）或程序集的嵌入资源；用户上传、需要按键存取的内容放对象存储。

## 启用

`Profiles.Full` 已装配文件系统存储；其他情况显式加入：

```bash
dotnet add package XiHan.Framework.ObjectStorage.FileSystem --prerelease
```

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

换成 S3 或兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等）时改装 `S3ObjectStorageFeature`。它是显式 Provider，与文件系统同时出现时顶替文件系统，业务代码注入的仍是 `IBlobStore`：

```csharp
using XiHan.Framework.ObjectStorage.S3;

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

`ServiceUrl` 为空时按 `Region` 访问 Amazon S3；各兼容服务对路径式与虚拟主机式寻址的支持不同，按服务商的说明设置 `ForcePathStyle`。

## 标识

| 标识 | 规则 | 例子 |
| --- | --- | --- |
| 容器 `ContainerName` | `^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$`，与 S3 桶名兼容 | `invoices` |
| 键 `BlobKey` | `/` 分段，1 到 1024 个字符；每段非空、不是 `.` 或 `..`；不含空白、控制字符与 `\`；首段不是 `.xihan`（保留给框架）；区分大小写 | `2026/10/inv-1.pdf` |
| 自定义元数据名 | `^[a-z0-9][a-z0-9-]{0,63}$`，值为任意字符串 | `uploaded-by` |

`ContainerName` 与 `BlobKey` 用构造或显式转换从 `string` 创建，不合法时抛出 `ArgumentException`，不会进入 Provider；`default` 值不是合法标识，读 `Value` 抛 `InvalidOperationException`。来自用户输入的键先用 `BlobKey.IsValid` 判断。

首段是 `.xihan` 的键（`BlobKey.ReservedSegment`，`.xihan` 本身与 `.xihan/…`，区分大小写）保留给框架自己的对象，例如 S3 的条件写入探测键 `.xihan/probe/conditional-write`：业务写不进、读不到、删不掉，各存储列举时跳过存储里已有的这类对象，所以它们不会混进业务列举。以 `.xihan` 开头的查询前缀不报错，列举结果总是空的。

## 最小示例

两个端点：`PUT /files/{name}` 把请求体存成对象，`GET /files/{**key}` 把对象流回客户端。失败结果交给宿主写成 Problem Details：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.FileSystem;
using XiHan.Framework.Primitives;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<FileSystemObjectStorageFeature>());

var app = builder.Build();
app.UseXiHan();

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
    var put = await store.PutAsync(new ContainerName("uploads"), new BlobKey(key), http.Request.Body, options, ct);
    if (!put.TryGetValue(out var metadata))
    {
        http.FindXiHanContext().ValueOrThrow().SetOutcome(put.Error!);
        return Results.Empty;
    }

    return Results.Ok(new { metadata.ETag, metadata.Length });
});

app.MapGet("/files/{**key}", async (string key, HttpContext http, IBlobStore store, CancellationToken ct) =>
{
    if (!BlobKey.IsValid(key))
    {
        return Results.NotFound();
    }

    var opened = await store.GetAsync(new ContainerName("uploads"), new BlobKey(key), ct);
    if (!opened.TryGetValue(out var blob))
    {
        http.FindXiHanContext().ValueOrThrow().SetOutcome(opened.Error!);
        return Results.Empty;
    }

    // 响应结束时释放 BlobContent，内容流随之关闭
    http.Response.RegisterForDisposeAsync(blob);
    return Results.Stream(blob.Content, blob.Metadata.ContentType);
});

app.Run();
```

运行后：`PUT /files/a.txt` 带正文返回 `{ "eTag": "…", "length": N }`，磁盘上出现 `<RootPath>/uploads/data/inbox/a.txt` 与 `meta/inbox/a.txt.json`；再 `PUT` 一次同名文件得到 409 的 Problem Details，`code` 是 `OBJECTSTORAGE_CONFLICT_001`；`GET /files/inbox/a.txt` 以原内容类型流回正文，不存在的键得到 404（`OBJECTSTORAGE_NOTFOUND_001`）。

## 写入

单条写入用扩展方法 `PutAsync(container, key, content, options, cancellationToken)`，`content` 是 `Stream` 或 `ReadOnlyMemory<byte>`，它转调批量方法。写入选项 `BlobPutOptions` 有内容类型 `ContentType`（缺省 `application/octet-stream`）、自定义元数据 `Metadata` 与 `Overwrite`（缺省 `true`）。

批量写入 `PutAsync(container, uploads, cancellationToken)` 按顺序逐项执行，返回与入参同序的逐项结果 `Result<BlobMetadata>`；两项之间检查取消，已完成的项不回滚。空列表返回空，重复的键按顺序各执行一次：

```csharp
BlobUpload[] uploads =
[
    new BlobUpload(new BlobKey("2026/10/inv-1.pdf"), first, new BlobPutOptions { ContentType = "application/pdf" }),
    new BlobUpload(new BlobKey("2026/10/inv-2.pdf"), second, new BlobPutOptions { ContentType = "application/pdf" }),
];
var results = await store.PutAsync(new ContainerName("invoices"), uploads, cancellationToken);

for (var i = 0; i < results.Count; i++)
{
    // 逐项结果与入参同序：第 i 项对应第 i 个上传
    if (!results[i].IsSuccess)
    {
        logger.LogWarning("Storing {Key} failed: {Code}", uploads[i].Key, results[i].Error?.Code);
    }
}
```

不覆盖写入在两个实现上都是原子的：多个写入方同时以 `Overwrite = false` 写同一个新键，只有一个成功，其余 `OBJECTSTORAGE_CONFLICT_001`。

## 读取

`GetAsync` 打开内容流与元数据，返回的 `BlobContent` 要释放（释放时关闭流，S3 一并释放响应）。只要元数据时用 `HeadAsync`，只判断存在用扩展方法 `ExistsAsync`，小对象一次读进内存用扩展方法 `ReadAllBytesAsync`。

元数据 `BlobMetadata` 有键、长度、内容类型、`ETag`（内容变化即变化）、最后修改时间与自定义元数据。不存在返回 `OBJECTSTORAGE_NOTFOUND_001`。只读一段的范围读取见[下一页](/object-storage/advanced#范围读取)。

## 删除

`DeleteAsync(container, keys, cancellationToken)` 批量删除，逐项结果 `Result<bool>` 与入参同序，值为 `true` 表示存在过并已删除，`false` 表示本来就不存在，失败时带错误。同一批里重复的键第二次报告不存在：

```csharp
var deleted = await store.DeleteAsync(new ContainerName("invoices"), [new BlobKey("2026/10/inv-1.pdf"), new BlobKey("2026/10/inv-2.pdf")], cancellationToken);
var removed = deleted.Count(static d => d.ValueOr(false));
```

单条删除用扩展方法 `DeleteAsync(container, key, cancellationToken)`，返回 `Result<bool>`。

## 列举

`ListAsync` 按键前缀列举，键的序数顺序，`PageSize` 为 1 到 1000（缺省 1000）；越界不在构造 `BlobQuery` 时抛出，列举返回 `OBJECTSTORAGE_VALIDATION_005`（HTTP 400），不访问存储——页大小常由请求绑定而来，越界是调用方可以改正的输入。续页标记 `ContinuationToken` 不透明，原样带回下一次查询：

```csharp
var query = new BlobQuery { Prefix = "2026/10/", PageSize = 100 };
while (true)
{
    var page = (await store.ListAsync(new ContainerName("invoices"), query, cancellationToken)).ValueOrThrow();
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

## 两个实现

| | 文件系统 | S3 |
| --- | --- | --- |
| 清单角色 | `role: default`，`Profiles.Full` 装配 | `role: provider` |
| 布局 | `<RootPath>/<container>/data/<key>` 存内容，`meta/<key>.json` 存元数据 | 容器即桶，键即对象键 |
| 原子性 | 先写同目录临时文件再原子替换；不覆盖写入先独占创建占位文件，再把内容替换上去 | 服务端保证；不覆盖写入带 `If-None-Match: *`，服务是否认它先探明（启动时探一次，没探明就后台重试，探明之前不覆盖写入返回 `OBJECTSTORAGE_UNAVAILABLE_004`） |
| `ETag` | 内容的 SHA-256 | 服务端给出 |
| 配额 | `MaxTotalBytes`（缺省 1 GiB）、`MaxBlobBytes`（缺省 256 MiB），超限 `OBJECTSTORAGE_UNAVAILABLE_002` | 无，由桶策略管理 |
| 健康项 | `objectstorage.filesystem.capacity`：用量达到总配额为 Degraded | `objectstorage.s3.connection`：按 `HealthProbeIntervalSeconds`（缺省 15 秒）重探连通性，并入条件写入的状态（连不上 Degraded；不认，以及启动之后才被拒绝或缺桶 Failed） |
| 预签名 | 宿主代收发 | 原生，客户端直连 |
| AOT | 兼容 | 不兼容（AWS SDK 基于运行时反射） |

文件系统存储单进程正确：启动时扫描目录得到已用量，之后在进程内计量，多个进程共享同一目录时配额只是近似。

S3 的不覆盖写入靠服务端的条件写入：不认条件写入的兼容服务、被拒的凭据、不存在的探测桶在启动时即启动失败（配置问题要早暴露）；只有启动时连不上照常启动，探明之前不覆盖写入返回 `OBJECTSTORAGE_UNAVAILABLE_004`；之后才探明不认的不覆盖写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`。阿里云 OSS、腾讯云 COS、Cloudflare R2 也只用这一个包，不为各家的私有禁止覆盖头做适配：它们是否认标准的 `If-None-Match: *` 要在接入前实测（缺省的条件写入探测即实测，或照 [ObjectStorage.S3](/packages/objectstorage-s3#云厂商的兼容服务) 用 curl 发两次条件写入），不认就关 `ConditionalWrites`——关掉后这个存储上 `Overwrite = false` 的写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`，「只在不存在时写入」的业务要改用不冲突的键或覆盖写入。

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
| `OBJECTSTORAGE_DENIED_002` | 条件写入探测被存储拒绝（S3：凭据无效、只读或无权写探测键；启动或建实例时即失败，启动之后才被拒时健康项 Failed、后台照常重试） |
| `OBJECTSTORAGE_CONFLICT_001` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | 对象存储不可达或 IO 失败，`reason` 只写异常类型的全名，完整异常进日志 |
| `OBJECTSTORAGE_UNAVAILABLE_002` | 配额或单对象大小超限 |
| `OBJECTSTORAGE_UNAVAILABLE_003` | 不覆盖写入在本存储上无法保证：S3 兼容服务不认条件写入，或存储关闭了条件写入（`ConditionalWrites = false`） |
| `OBJECTSTORAGE_UNAVAILABLE_004` | 不覆盖写入暂不可用：S3 兼容服务是否认条件写入尚未确认（HTTP 503，探测通过后自动恢复） |

HTTP 宿主按错误码的类别段映射状态码，见[结果与错误处理](/web/errors)。标识不合法属于编程错误，构造时抛出 `ArgumentException`。

## 常见问题

### 开发环境没有 S3 怎么办

默认实现就是文件系统，不需要任何外部服务；`Profiles.Full` 已装配。要和生产一样走 S3 协议时本地跑一个 MinIO，`ServiceUrl` 指向它、`ForcePathStyle` 为 `true`。

### 文件名是用户给的，能直接当键吗

先 `BlobKey.IsValid` 判断，不合法的直接拒绝；更稳的做法是自己生成键（日期加 ID），把原文件名放进自定义元数据或业务表。键不能含 `\`、空白与控制字符，也不能以 `.xihan` 开头。

### 怎样「只在不存在时写入」

`BlobPutOptions.Overwrite = false`，已存在返回 `OBJECTSTORAGE_CONFLICT_001`。两个实现都保证并发下只有一个成功；S3 兼容服务要认条件写入，不认的服务上这个选项一律失败，改用不冲突的键。

### 读取大文件会不会占满内存

`GetAsync` 返回的是流，边读边写响应即可，不要用 `ReadAllBytesAsync`；记得释放 `BlobContent`。只需一段时传 `BlobRange`，见下一页。

### 删除不存在的对象算失败吗

不算。`DeleteAsync` 的逐项结果 `Outcome` 为 `false` 表示本来就不存在，只有 IO 或服务错误才带错误；重复删除是幂等的。

## 相关包与下一步

- [ObjectStorage.Abstractions](/packages/objectstorage-abstractions)：契约、标识与错误码
- [ObjectStorage.FileSystem](/packages/objectstorage-filesystem)：默认实现、布局与配额
- [ObjectStorage.S3](/packages/objectstorage-s3)：S3 与兼容服务、条件写入探测
- 下一步：[分片、预签名与命名实例](/object-storage/advanced)
