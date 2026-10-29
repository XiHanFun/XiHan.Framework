# XiHan.Framework.ObjectStorage.Abstractions

对象存储契约（Ring 2，RFC-0012）。能力 `objectstorage.blob@1` 由 Provider 提供；默认实现 `ObjectStorage.FileSystem`。

| 契约 | 用途 |
| --- | --- |
| `ContainerName`、`BlobKey`、`BlobPutOptions` | 容器名（S3 桶名文法）、`/` 分段的键、写入选项（内容类型、自定义元数据、是否覆盖）；不合法在构造时抛出 |
| `IBlobStore` | `Put(container, uploads[])`、`Get`、`Head`、`Delete(container, keys[])`、`List(container, query)`；写入与删除是批量的，逐项结果 |
| `BlobStoreExtensions` | 单条便捷方法：`Put(stream | bytes)`、`Delete(key)`、`Exists`、`ReadAllBytes`，都转调批量方法 |
| `BlobMetadata`、`BlobContent`、`BlobPage`、`BlobQuery` | 元数据（长度、内容类型、ETag、修改时间、自定义元数据）、读取到的流、分页列举 |
| `BlobRange`、`BlobContentRange` | 范围读取（ADR-0049）：`GetAsync(container, key, new BlobRange(offset, length?))`，`BlobContent.Range` 给出实际范围，元数据长度仍是总长；起点越界 `OBJECTSTORAGE_VALIDATION_001` |
| `Multipart.IBlobMultipartStore` | 分片上传（`objectstorage.multipart@1`）：`Start` / `UploadPart`（1–10000，可乱序、可重传）/ `ListParts` / `Complete`（升序、ETag 相符、非末片 ≥ 5 MiB）/ `Abort`；`BlobMultipartUpload` 可持久化后续传 |
| `Signing.IBlobUrlSigner` | 预签名地址（`objectstorage.presign@1`）：`BlobUrlRequest.ForDownload` / `ForUpload` / `ForPart`，缺省 30 分钟；`BlobSignedUrl` 带客户端必须发送的头；`BlobContentDisposition` 统一下载文件名的写法 |

```csharp
var put = await store.PutAsync("invoices", "2026/10/inv-1.pdf", stream, new BlobPutOptions { ContentType = "application/pdf" }, ct);
var page = await store.ListAsync("invoices", new BlobQuery { Prefix = "2026/10/", PageSize = 100 }, ct);
var deleted = await store.DeleteAsync("invoices", [key1, key2], ct);   // 逐项结果：存在过 / 不存在 / 失败
```

不存在 → `OBJECTSTORAGE_NOTFOUND_001`；已存在且不覆盖 → `OBJECTSTORAGE_CONFLICT_001`；IO 失败 → `OBJECTSTORAGE_UNAVAILABLE_001`；配额 → `OBJECTSTORAGE_UNAVAILABLE_002`；范围不可满足 → `OBJECTSTORAGE_VALIDATION_001`；分片清单不合法 → `OBJECTSTORAGE_VALIDATION_002`；上传不存在 → `OBJECTSTORAGE_NOTFOUND_002`；签名无效或过期 → `OBJECTSTORAGE_DENIED_001`。

```csharp
// 断点续传：服务端发起，客户端按片直传，服务端完成
var upload = (await multipart.StartAsync("videos", key, new BlobPutOptions { ContentType = "video/mp4" }, ct)).ValueOrThrow();
var partUrl = (await signer.SignAsync("videos", key, BlobUrlRequest.ForPart(upload, 1), ct)).ValueOrThrow();
var done = await multipart.CompleteAsync(upload, parts, ct);
// 限时下载链接（缺省 30 分钟）
var link = (await signer.SignAsync("invoices", key, BlobUrlRequest.ForDownload(downloadFileName: "发票.pdf"), ct)).ValueOrThrow().Url;
```

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Localization.Abstractions`。被 `ObjectStorage.FileSystem`、`ObjectStorage.Conformance` 引用。

## 状态

P3：可用。
