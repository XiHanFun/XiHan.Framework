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
- 写入：`Overwrite = false` 先 HEAD 再带 `If-None-Match: *` 写入，由服务端保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`；服务是否认条件写入要先探明（下一条），探明之前不覆盖写入不放行；不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。`EnsureBuckets = true` 时桶不存在就建桶再重试一次，默认由基础设施预先建桶。
- 条件写入探测（ADR-0089、ADR-0090）：`ConditionalWrites`（缺省 `true`）开启时，在 `HealthBucket`（这时必填，没配即装配失败 / 命名实例 `OBJECTSTORAGE_VALIDATION_003`）里对探测键 `.xihan/probe/conditional-write` 带 `If-None-Match: *` 写两次，第二次必须 412；提供分片上传时再以同一个键带条件完成一次分片上传，必须 412，随即放弃该上传。探测键是空对象、写入后不删除——任何一次 412 都证明服务认条件写入，所以并发启动、反复启动、后台重试结论相同；凭据只需要该键的 `s3:PutObject` 与 `s3:AbortMultipartUpload`；它在保留前缀 `.xihan`（`BlobKey.ReservedSegment`）下，不在键文法之内，业务列举跳过它（ADR-0090；ADR-0089 起就是这个名字，桶里已有的探测键无需迁移，也不必删除）。单次探测以 `HealthProbeIntervalSeconds` 为超时。
- 探测的时机与结论（ADR-0090、ADR-0093）：缺省实例在启动时（与健康项的首次探测并行）、命名实例在建出时探一次。探明不认（第二次没被拒或 501，`OBJECTSTORAGE_UNAVAILABLE_003`）、被拒绝（403：凭据无效、只读或无权写探测键，`OBJECTSTORAGE_DENIED_002`）、探测桶不存在（`OBJECTSTORAGE_NOTFOUND_001`，开 `EnsureBuckets` 时先建桶）都是配置问题，启动失败（`KERNEL_HOSTING_002` ← 对应码）或不建命名实例（注册表返回对应码）。只有连不上或超时（`OBJECTSTORAGE_UNAVAILABLE_001`）是"尚未确认"：照常启动、照常建出实例，在后台重试——1 秒后，之后间隔加倍，最长 `HealthProbeIntervalSeconds`，按注入的 `TimeProvider` 计时，有了结论即停，应用停止或实例释放时也停。尚未确认时 `Overwrite = false` 的写入与分片上传的发起、完成：有探测在进行就等它，没有而距上一次探测满 1 秒就地再探一次（每个实例同一时刻只有一个探测），仍未确认返回 `OBJECTSTORAGE_UNAVAILABLE_004`（HTTP 503，`reason` 是最近一次探测失败的错误码），不访问写入目标、不退回先查再写；服务恢复后自动通过，不必重启。启动之后才探明不认：不覆盖写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`，记 Error 日志，换服务或关闭条件写入后重启。启动时连不上、连上时才被拒绝或缺桶（凭据已被吊销、桶被删，或配置本来就错）：应用不停止，仍是尚未确认、照常重试，不覆盖写入仍是 `OBJECTSTORAGE_UNAVAILABLE_004`（`reason` 为 `DENIED_002` / `NOTFOUND_001`），健康项 Failed，记 Error 日志；权限改好、桶建好后自动通过（已定，ADR-0097：不改为停止重试或只记 Degraded）。已确认之后凭据被吊销不再探测，读写按下面的错误映射失败、连通性 HEAD 让健康项 Degraded。实测 RustFS 1.0.4、MinIO 通过，Garage v2.5.0、Zenko CloudServer 8.2.7 不认条件写入、启动失败（Zenko 于 2026-11-02 补跑，见 ADR-0093）。
- 云厂商的兼容服务（ADR-0090）：阿里云 OSS、腾讯云 COS、Cloudflare R2 等同样只用本包，不为各家的私有请求头（OSS 的 `x-oss-forbid-overwrite`、COS 的 `x-cos-forbid-overwrite`）做适配，不覆盖写入只认标准的 `If-None-Match: *`。这几家是否认它框架没有实测，接入前要实测：直接用缺省的探测（启动时探明不认即启动失败，`OBJECTSTORAGE_UNAVAILABLE_003`；连不上时看健康项数据 `conditionalWrites` 是否变成 `confirmed`），或用 curl 对同一个键发两次带 `If-None-Match: *` 的 `PutObject`，第二次必须 412。不认就设 `ConditionalWrites = false`（命名实例的设置同名，可按实例关闭），后果见下一条。
- 关闭条件写入（`ConditionalWrites = false`）：只用于明确不需要不覆盖写入的应用（只覆盖写入、凭据只读，或服务实测不认条件写入）。不探测、`HealthBucket` 可不配；`Overwrite = false` 的写入与分片上传的发起、完成一律返回 `OBJECTSTORAGE_UNAVAILABLE_003`，不访问存储——不做没有保证的先查再写；"只在不存在时写入"在这个存储上不再可用，需要它的业务改用不会冲突的键（如带随机后缀）或改为覆盖写入（`BlobPutOptions` 缺省就是覆盖写入）。
- 读取：`Get` 的内容流释放时连同响应一起释放；`Head` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页（S3 保证键的序数顺序），续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为默认值、自定义元数据为空，需要时再 `Head`；不符合键文法的对象（以 `/` 结尾的"目录"占位、保留前缀 `.xihan` 下的框架对象如探测键）跳过，所以一页可能少于 `PageSize` 条、甚至为空而仍带续页标记；桶不存在视作空容器。
- 错误：不存在的键与桶映射为 `OBJECTSTORAGE_NOTFOUND_001`，其余服务端错误、网络错误与 SDK 超时映射为 `OBJECTSTORAGE_UNAVAILABLE_001`；调用方的取消原样抛出。没有配额（由桶策略管理）。
- 健康项 `objectstorage.s3.connection`：启动时与条件写入探测并行同步探一次（S3 连不上时启动只等一个超时），之后按 `HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果；对 `HealthBucket` HEAD（桶不存在、无权限或服务不可达都算失败），关闭条件写入且没配时列桶；失败或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 只写异常类型，完整异常进日志），应用仍在服务。条件写入的状态即时并入（数据 `conditionalWrites`：`confirmed` / `unconfirmed` / `unsupported` / `disabled`）：尚未确认时错误是最近一次探测失败的错误码（HEAD 成功也一样）：连不上 Degraded，启动之后才被拒绝或缺桶 Failed（`DENIED_002` / `NOTFOUND_001`）；启动之后才探明不认时条目 Failed（`OBJECTSTORAGE_UNAVAILABLE_003`）。Failed 的条目让应用聚合为 Degraded，应用仍在服务。
- 范围读取、分片与预签名（ADR-0049，同时提供 `objectstorage.multipart@1` 与 `objectstorage.presign@1`）：范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长，起点越界（416）报 `OBJECTSTORAGE_VALIDATION_001`；分片直接映射 S3 multipart，`ListParts` 分页取全，不覆盖的发起与完成要求条件写入已确认（探测同时核对条件完成），完成先 HEAD 再带 `If-None-Match: *`，服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）报 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 报 `OBJECTSTORAGE_NOTFOUND_002`；预签名经 SDK 本地签出（签名 V4，端点是 http 时按 http 签），下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号，客户端直连存储。
- 命名实例过出站地址策略（RFC-0008、ADR-0117）：定义可能来自应用的库，端点不可信，`S3BlobStoreProvider` 建出的客户端只能连公网地址，自建存储要放行（`XiHan:Security:Outbound`）。被拒绝与连不上同样处理，读写返回 `OBJECTSTORAGE_UNAVAILABLE_001`。缺省实例的端点是运维配的，不过策略。
- 命名实例（ADR-0050）：特性总是登记 Provider `S3BlobStoreProvider`（名 `s3`）——缺省实例另选了文件系统、能力全部被顶替（经 `IShadowAwareFeature.ConfigureShadowed`）或其余能力只是兜底选出时只登记它，不读本配置节与绑定、不建缺省客户端、不探测（ADR-0075）；设置的键同本配置节（不含 `HealthProbeIntervalSeconds`，探测间隔由注册表统一），未知键报错；每个实例按自己的端点、区域与密钥建客户端，不读绑定，`ConditionalWrites` 开启时建出时探测条件写入（探明不认、被拒绝、探测桶不存在都不登记，注册表返回 `OBJECTSTORAGE_UNAVAILABLE_003` / `DENIED_002` / `NOTFOUND_001`，下次取用重新探测；只有连不上的照常建出并在后台重试），实例被替换、移除或应用停止时停止重试、释放客户端（经公开构造函数建的 `S3BlobStore` 不探测、不拥有客户端）；实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；健康探测对实例的 `HealthBucket` HEAD（没配时列桶），连通之后再看条件写入是否已确认，尚未确认或不认时 Degraded。
- 缺省实例的一致性（ADR-0075、ADR-0082）：缺省的 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 必须落在同一存储上，只约束被要求的契约（有特性依赖，或 `XiHan:Providers` / `UseProvider` 指定）。只把 `objectstorage.blob` 指定给文件系统时，本包的分片上传与预签名只是兜底选出，本包按被顶替处理（只登记命名实例 Provider，不读配置节与绑定、不建客户端、不探测），不必装 `HostedBlobUrlFeature`；有特性依赖分片上传或预签名、或配置把它们指定给本包时，它们是被要求的，与缺省对象存储不一致即 `OBJECTSTORAGE_VALIDATION_004`（`BlobStoreSelection.ProvidesDefaultInstance`）启动失败——这时把预签名指定给 `xihan.hosting.blob-urls`、分片上传指定给文件系统。
- 不满足 AOT 分析器（AWS SDK 的请求编组基于运行时反射）。

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions` 与 `AWSSDK.S3`。

## 测试

`ObjectStorage.S3.Tests` 在设置环境变量 `XIHAN_TEST_S3`（服务端点，如 `http://localhost:9000`）时对真实服务跑 `ObjectStorage.Conformance` 全部用例（含命名实例的 `BlobStoreProviderConformance`）与条件写入探测的用例（探测桶 `xihan-probe` 由用例建出），`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `rustfsadmin`；未设置时跳过。`XIHAN_TEST_S3_UNCONDITIONAL` 指向不认条件写入的兼容服务（如 `docker run -p 8000:8000 -e SCALITY_ACCESS_KEY_ID=rustfsadmin -e SCALITY_SECRET_ACCESS_KEY=rustfsadmin -e REMOTE_MANAGEMENT_DISABLE=1 zenko/cloudserver:8.2.7`）时，核对它让启动与命名实例的建出都以 `OBJECTSTORAGE_UNAVAILABLE_003` 失败；指向 RustFS 时另核对密钥错、探测桶不存在让启动与命名实例的建出失败（ADR-0093）。探测的判定、失败原因、超时与关闭条件写入，启动时被拒绝、缺桶即启动失败，以及启动时连不上的照常启动、后台退避重试、写入时就地探测与恢复、之后才探明不认或才被拒绝 / 缺桶（ADR-0090、ADR-0093），由内存替身 `FakeS3Client` 加 `FakeTimeProvider` 覆盖，不连服务、不靠真实等待。本地用 `docker run -p 9000:9000 rustfs/rustfs:1.0.4` 起一个 S3 兼容的 RustFS 即可（不设 `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY` 时根凭据就是 `rustfsadmin`；MinIO 社区版已停发镜像，选型见 ADR-0086）。

## 状态

P5：可用，通过 `ObjectStorage.Conformance`（RustFS 1.0.4、MinIO）。
