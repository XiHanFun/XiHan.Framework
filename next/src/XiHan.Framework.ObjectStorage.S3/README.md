# XiHan.Framework.ObjectStorage.S3

对象存储的 S3 Provider（Ring 3，`role: provider`）。提供 `objectstorage.blob@1`（RFC-0012）：容器即桶，键即对象键；经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等）。

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<S3ObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "S3": { "ServiceUrl": "http://localhost:9000", "Region": "us-east-1", "AccessKey": "rustfsadmin", "SecretKey": "rustfsadmin", "ForcePathStyle": true, "EnsureBuckets": false, "HealthBucket": "uploads" } } } }
```

绑定 `objectstorage`（`blob`，可选；类型取 RFC-0016 的绑定类型词表）存在时逐项覆盖配置：主值是服务端点，属性 `region`、`accessKey`、`secretKey`、`forcePathStyle`；空白的主值与属性不算给出（RFC-0016「资源绑定与配置节」）。

## 规则

- `ServiceUrl` 为空时按 `Region` 访问 Amazon S3；有值时指向兼容服务，`Region` 作签名区域（默认 `us-east-1`），并只在服务端要求时计算 / 校验校验和（多数兼容服务不支持分块尾部校验和）。`AccessKey` / `SecretKey` 都为空时走 SDK 默认凭据链（环境变量、配置文件、实例角色）。应用自己注册的 `IAmazonS3` 优先于本特性创建的客户端。
- 写入：`Overwrite = false` 先 HEAD 再带 `If-None-Match: *` 写入，由服务端保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`；服务是否认条件写入在启动时探测（下一条）；不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。`EnsureBuckets = true` 时桶不存在就建桶再重试一次，默认由基础设施预先建桶。
- 条件写入探测（ADR-0089）：`ConditionalWrites`（缺省 `true`）开启时，缺省实例在启动时、命名实例在建出时，在 `HealthBucket`（这时必填，没配即装配失败 / 命名实例 `OBJECTSTORAGE_VALIDATION_003`）里对探测键 `.xihan/probe/conditional-write` 带 `If-None-Match: *` 写两次，第二次必须 412；提供分片上传时再以同一个键带条件完成一次分片上传，必须 412，随即放弃该上传。探测键是空对象、写入后不删除——任何一次 412 都证明服务认条件写入，所以并发启动、反复启动结论相同；凭据只需要该键的 `s3:PutObject` 与 `s3:AbortMultipartUpload`；按空前缀列举该桶时会看到它。失败即启动失败（`KERNEL_HOSTING_002`）或不建命名实例，原因各有错误码：不认条件写入（第二次没被拒或 501）`OBJECTSTORAGE_UNAVAILABLE_003`、被拒绝（403：凭据无效、只读或无权写探测键）`OBJECTSTORAGE_DENIED_002`、探测桶不存在 `OBJECTSTORAGE_NOTFOUND_001`（开 `EnsureBuckets` 时先建桶）、连不上或超时（`HealthProbeIntervalSeconds`）`OBJECTSTORAGE_UNAVAILABLE_001`。实测 RustFS 1.0.4、MinIO 通过，Garage v2.5.0、Zenko CloudServer 8.2.7 不认条件写入、启动失败。
- 关闭条件写入（`ConditionalWrites = false`）：只用于明确不需要不覆盖写入的应用（只覆盖写入、凭据只读）。不探测、`HealthBucket` 可不配；`Overwrite = false` 的写入与分片上传的发起、完成一律返回 `OBJECTSTORAGE_UNAVAILABLE_003`，不访问存储——不做没有保证的先查再写。
- 读取：`Get` 的内容流释放时连同响应一起释放；`Head` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页（S3 保证键的序数顺序），续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为默认值、自定义元数据为空，需要时再 `Head`；不符合键文法的对象（如以 `/` 结尾的"目录"占位）跳过；桶不存在视作空容器。
- 错误：不存在的键与桶映射为 `OBJECTSTORAGE_NOTFOUND_001`，其余服务端错误、网络错误与 SDK 超时映射为 `OBJECTSTORAGE_UNAVAILABLE_001`；调用方的取消原样抛出。没有配额（由桶策略管理）。
- 健康项 `objectstorage.s3.connection`：在条件写入探测通过之后启动，启动时同步探一次，之后按 `HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果；对 `HealthBucket` HEAD（桶不存在、无权限或服务不可达都算失败），关闭条件写入且没配时列桶；不重复条件写入探测；失败或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 是 SDK 的异常消息），应用仍在服务。
- 范围读取、分片与预签名（ADR-0049，同时提供 `objectstorage.multipart@1` 与 `objectstorage.presign@1`）：范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长，起点越界（416）报 `OBJECTSTORAGE_VALIDATION_001`；分片直接映射 S3 multipart，`ListParts` 分页取全，不覆盖的完成先 HEAD 再带 `If-None-Match: *`（服务认条件完成由启动探测确认），服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）报 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 报 `OBJECTSTORAGE_NOTFOUND_002`；预签名经 SDK 本地签出（签名 V4，端点是 http 时按 http 签），下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号，客户端直连存储。
- 命名实例（ADR-0050）：特性总是登记 Provider `S3BlobStoreProvider`（名 `s3`）——缺省实例另选了文件系统、能力全部被顶替（经 `IShadowAwareFeature.ConfigureShadowed`）或其余能力只是兜底选出时只登记它，不读本配置节与绑定、不建缺省客户端、不探测（ADR-0075）；设置的键同本配置节（不含 `HealthProbeIntervalSeconds`，探测间隔由注册表统一），未知键报错；每个实例按自己的端点、区域与密钥建客户端，不读绑定，`ConditionalWrites` 开启时建出前探测条件写入（不通过不登记，注册表返回探测的错误码，下次取用重新探测），实例被替换、移除或应用停止时释放客户端（经公开构造函数建的 `S3BlobStore` 不拥有客户端）；实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；健康探测对实例的 `HealthBucket` HEAD，没配时列桶。
- 缺省实例的一致性（ADR-0075、ADR-0082）：缺省的 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 必须落在同一存储上，只约束被要求的契约（有特性依赖，或 `XiHan:Providers` / `UseProvider` 指定）。只把 `objectstorage.blob` 指定给文件系统时，本包的分片上传与预签名只是兜底选出，本包按被顶替处理（只登记命名实例 Provider，不读配置节与绑定、不建客户端、不探测），不必装 `HostedBlobUrlFeature`；有特性依赖分片上传或预签名、或配置把它们指定给本包时，它们是被要求的，与缺省对象存储不一致即 `OBJECTSTORAGE_VALIDATION_004`（`BlobStoreSelection.ProvidesDefaultInstance`）启动失败——这时把预签名指定给 `xihan.hosting.blob-urls`、分片上传指定给文件系统。
- 不满足 AOT 分析器（AWS SDK 的请求编组基于运行时反射）。

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions` 与 `AWSSDK.S3`。

## 测试

`ObjectStorage.S3.Tests` 在设置环境变量 `XIHAN_TEST_S3`（服务端点，如 `http://localhost:9000`）时对真实服务跑 `ObjectStorage.Conformance` 全部用例（含命名实例的 `BlobStoreProviderConformance`）与条件写入探测的用例（探测桶 `xihan-probe` 由用例建出），`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `rustfsadmin`；未设置时跳过。`XIHAN_TEST_S3_UNCONDITIONAL` 指向不认条件写入的兼容服务（如 `docker run -p 8000:8000 -e SCALITY_ACCESS_KEY_ID=rustfsadmin -e SCALITY_SECRET_ACCESS_KEY=rustfsadmin -e REMOTE_MANAGEMENT_DISABLE=1 zenko/cloudserver:8.2.7`）时，核对它让启动与命名实例的建出都以 `OBJECTSTORAGE_UNAVAILABLE_003` 失败。探测的判定、失败原因、超时与关闭条件写入由内存替身覆盖，不连服务。本地用 `docker run -p 9000:9000 rustfs/rustfs:1.0.4` 起一个 S3 兼容的 RustFS 即可（不设 `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY` 时根凭据就是 `rustfsadmin`；MinIO 社区版已停发镜像，选型见 ADR-0086）。

## 状态

P5：可用，通过 `ObjectStorage.Conformance`（RustFS 1.0.4，此前 MinIO）。
