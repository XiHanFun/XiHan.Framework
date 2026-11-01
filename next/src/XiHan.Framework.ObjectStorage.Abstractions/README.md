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
| `Stores.BlobStores`、`BlobStoreDefinition`、`IBlobStoreProvider` | 命名存储实例（ADR-0050）：定义是实例名、Provider 名（`filesystem` / `s3`）与设置（键同 Provider 配置节，未知键报错）；注册表按名缓存，`GetAsync(name)` 取已建的或配置节 `XiHan:ObjectStorage:Stores:<name>` 定义的，`GetOrAddAsync(definition)` 同定义复用、定义变了替换并释放旧实例，`RemoveAsync` 释放；每个实例一条健康项 `objectstorage.store.<name>`。存储包装配时经 `AddBlobStoreProvider<T>(configuration)` 登记 Provider，被别的存储顶替时也登记（`IShadowAwareFeature`） |
| `Stores.BlobStoreSelection` | 缺省实例的一致性（ADR-0075、ADR-0082）：`ProvidesDefaultInstance(node)` 在存储包的 `Configure` 开头核对——被选为 `objectstorage.blob` 是缺省存储；没被选为对象存储却被要求（有依赖或经配置 / 代码指定，`ResolvedNode.IsRequestedFor`）提供 `objectstorage.multipart` 或 `objectstorage.presign` 时 `OBJECTSTORAGE_VALIDATION_004` 启动失败；两者都只是兜底选出时返回 `false`，存储包按被顶替处理；宿主代收发的签名器不受约束 |

```csharp
var put = await store.PutAsync("invoices", "2026/10/inv-1.pdf", stream, new BlobPutOptions { ContentType = "application/pdf" }, ct);
var page = await store.ListAsync("invoices", new BlobQuery { Prefix = "2026/10/", PageSize = 100 }, ct);
var deleted = await store.DeleteAsync("invoices", [key1, key2], ct);   // 逐项结果：存在过 / 不存在 / 失败
```

实例不存在 → `OBJECTSTORAGE_NOTFOUND_003`；实例定义不合法 → `OBJECTSTORAGE_VALIDATION_003`；缺省存储的能力选择不一致（装配时）→ `OBJECTSTORAGE_VALIDATION_004`；不存在 → `OBJECTSTORAGE_NOTFOUND_001`；已存在且不覆盖 → `OBJECTSTORAGE_CONFLICT_001`；IO 失败 → `OBJECTSTORAGE_UNAVAILABLE_001`；配额 → `OBJECTSTORAGE_UNAVAILABLE_002`；范围不可满足 → `OBJECTSTORAGE_VALIDATION_001`；分片清单不合法 → `OBJECTSTORAGE_VALIDATION_002`；列举的页大小不在 1～1000（不在构造 `BlobQuery` 时抛出，Provider 经 `BlobQuery.Validate()` 返回）→ `OBJECTSTORAGE_VALIDATION_005`；上传不存在 → `OBJECTSTORAGE_NOTFOUND_002`；签名无效或过期 → `OBJECTSTORAGE_DENIED_001`；不覆盖写入在本存储上无法保证（S3 服务不认条件写入，或存储关闭了条件写入）→ `OBJECTSTORAGE_UNAVAILABLE_003`；条件写入探测被拒绝 → `OBJECTSTORAGE_DENIED_002`（ADR-0089）。不覆盖写入必须原子：并发的不覆盖写入同一个新键只有一个成功。Provider 建命名实例前的探测不通过时抛出携带错误码的 `FrameworkException`，注册表转成同码的失败结果、不登记实例。

```csharp
// 断点续传：服务端发起，客户端按片直传，服务端完成
var upload = (await multipart.StartAsync("videos", key, new BlobPutOptions { ContentType = "video/mp4" }, ct)).ValueOrThrow();
var partUrl = (await signer.SignAsync("videos", key, BlobUrlRequest.ForPart(upload, 1), ct)).ValueOrThrow();
var done = await multipart.CompleteAsync(upload, parts, ct);
// 限时下载链接（缺省 30 分钟）
var link = (await signer.SignAsync("invoices", key, BlobUrlRequest.ForDownload(downloadFileName: "发票.pdf"), ct)).ValueOrThrow().Url;
```

```csharp
// 命名实例：定义来自应用自己的库（存储配置），改了就替换
var definition = new BlobStoreDefinition("tenant-42-oss", "s3", new Dictionary<string, string>
{
    ["ServiceUrl"] = "https://oss-cn-hangzhou.aliyuncs.com", ["AccessKey"] = accessKey, ["SecretKey"] = secretKey,
});
var store = (await stores.GetOrAddAsync(definition, ct)).ValueOrThrow();
var archive = (await stores.GetAsync("archive", ct)).ValueOrThrow();   // XiHan:ObjectStorage:Stores:archive 定义的
```

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.Configuration.Abstractions` 与 `Microsoft.Extensions.DependencyInjection.Abstractions`。被 `ObjectStorage.FileSystem`、`ObjectStorage.S3`、`ObjectStorage.Conformance` 与 `Hosting.AspNetCore` 引用。

## 状态

P3：可用。
