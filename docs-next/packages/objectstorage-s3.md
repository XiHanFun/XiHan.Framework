# XiHan.Framework.ObjectStorage.S3

对象存储的 S3 Provider：容器即桶，键即对象键，经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等），同时提供原生分片上传与预签名地址。多实例部署、对象量大或需要客户端直连存储时使用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.ObjectStorage.S3` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `objectstorage.blob@1`、`objectstorage.multipart@1`、`objectstorage.presign@1`（`role: provider`）；依赖 `kernel.hosting@1`；可选绑定 `objectstorage`（`blob`） |
| 特性入口 | `S3ObjectStorageFeature` |
| 配置节 | `XiHan:ObjectStorage:S3` |
| 依赖 | `XiHan.Framework.ObjectStorage.Abstractions`；`AWSSDK.S3` 4.0.102.3 |
| AOT | 不兼容（AWS SDK 的请求编组基于运行时反射） |
| 稳定性 | Preview |

## 概述

本包是显式 Provider，装配后顶替默认的文件系统存储，三个能力都由同一个 `S3BlobStore` 实例提供。客户端由特性按选项创建，停止时释放；应用自己注册了 `IAmazonS3`（自定义凭据链、代理、重试策略）时沿用应用的。各家兼容服务共用这一个包，不再为每家云单独做包。

`ServiceUrl` 为空时按 `Region` 访问 Amazon S3；有值时指向兼容服务，`Region` 作签名区域，并只在服务端要求时计算与校验校验和（多数兼容服务不支持分块尾部校验和）。`AccessKey` 与 `SecretKey` 都为空时走 SDK 的默认凭据链（环境变量、配置文件、实例角色）。

## 何时使用

- 生产环境、多实例部署、需要预签名直传与直下：用本包。
- 开发与单实例部署：默认的 [ObjectStorage.FileSystem](./objectstorage-filesystem) 即可。
- 应用要以 Native AOT 发布：本包不满足 AOT 分析器，改用文件系统存储，或应用实现自己的 `IBlobStore`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.ObjectStorage.S3" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
builder.AddXiHan(xihan => xihan.UseFullProfile().UseFeature<S3ObjectStorageFeature>());
```

本包是缺省存储时不要再装 `HostedBlobUrlFeature`：两者都提供 `objectstorage.presign@1`，S3 自己能签名。缺省存储另选了文件系统、本包只为命名实例而装时，指定 `objectstorage.blob` = `xihan.objectstorage.filesystem` 即可：本包的分片上传与预签名只是兜底选出（没人依赖、没人指定），本包按被顶替处理，只登记 `s3` 命名实例 Provider，不读配置节与绑定、不建客户端、不探测，也不必装 `HostedBlobUrlFeature`。有特性依赖分片上传或预签名、或配置把它们指定给本包时，它们是被要求的，与缺省对象存储不一致即 `OBJECTSTORAGE_VALIDATION_004` 启动失败（缺省的三个契约必须同一存储）；要缺省的预签名就装 `HostedBlobUrlFeature` 并把 `objectstorage.presign` 指定给 `xihan.hosting.blob-urls`。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServiceUrl` | 绝对 http / https 地址 | 空（Amazon S3） | 兼容服务的端点，如 `http://localhost:9000` |
| `Region` | `string` | `us-east-1` | 访问 Amazon S3 时选端点，访问兼容服务时作签名区域 |
| `AccessKey` | `string` | 空 | 访问密钥 ID；与 `SecretKey` 都为空时走 SDK 默认凭据链；与 `SecretKey` 一样在选项的 `ToString()` 里打印为 `***` |
| `SecretKey` | `string` | 空 | 访问密钥 |
| `ForcePathStyle` | `bool` | `true` | 桶名放在路径而不是主机名里；各兼容服务对两种寻址的支持不同，按服务商说明设置 |
| `EnsureBuckets` | `bool` | `false` | 写入遇到桶不存在时建桶再重试一次；缺省由基础设施预先建桶 |
| `HealthBucket` | `string` | 空 | 框架探测用的桶：健康探测对它做 HEAD（同时验证凭据对该桶的权限）；`ConditionalWrites` 开启时在其中探测条件写入，这时必填；关闭且为空时健康探测列桶 |
| `ConditionalWrites` | `bool` | `true` | 不覆盖写入由条件写入（`If-None-Match: *`）保证：开启时探测服务是否支持，启动时就探明不支持即启动失败，连不上等情况照常启动、后台重试；关闭表示应用不需要不覆盖写入，`Overwrite = false` 的写入一律失败（见下文「条件写入探测」） |
| `HealthProbeIntervalSeconds` | 正数 | 15 | 健康探测的间隔，也是单次探测的超时；条件写入尚未确认时后台重试的间隔从 1 秒起倍增，最长也是它 |

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
        "EnsureBuckets": false,
        "HealthBucket": "uploads"
      }
    }
  }
}
```

绑定 `objectstorage` 存在时逐项覆盖配置：主值是服务端点，属性 `region`、`accessKey`、`secretKey`、`forcePathStyle`；空白的主值与属性不算给出，照读配置节。Kubernetes 里按 Service Binding 规范挂载的目录每个属性一个文件：

```text
/bindings/objectstorage/type          blob
/bindings/objectstorage/uri           http://s3.internal:9000
/bindings/objectstorage/accessKey     dev-access-key
/bindings/objectstorage/secretKey     dev-secret-key
```

绑定的解析规则见[配置与选项](../guide/configuration#资源绑定)。命名实例（Provider 名 `s3`）的设置用同样的键，不含 `HealthProbeIntervalSeconds`（探测间隔由注册表统一），也不读绑定。

## 条件写入探测

不覆盖写入（`Overwrite = false`）在 S3 上靠条件写入 `If-None-Match: *` 保证原子。有的兼容服务不认这个请求头，照常写入：先 HEAD 再写的两个写入方都会成功，后写的静默覆盖先写的（实测 Garage、Zenko CloudServer 上 8 个并发写入方全部"成功"）。所以本包在不覆盖写入之前先探明服务是否认它（ADR-0089、ADR-0090）：

- 缺省实例在启动时、健康探针之前探一次；命名实例在建出时探一次，每个实例各探各的。
- 在 `HealthBucket` 里对探测键 `.xihan/probe/conditional-write` 带 `If-None-Match: *` 写两次，第二次必须被拒（412）；提供分片上传时再以同一个键带条件完成一次分片上传，也必须被拒，被拒的上传随即放弃。
- 探测键是空对象，写入后不删除：任何一次 412 都证明服务认条件写入，键已存在时第一次写入就被拒，所以多个实例同时启动、反复启动、后台重试都得到同一结论。凭据只需要对这个键的 `s3:PutObject` 与 `s3:AbortMultipartUpload`，不需要删除或列举；按空前缀列举该桶时会看到它。
- 单次探测以 `HealthProbeIntervalSeconds` 为超时；`EnsureBuckets` 开启时探测桶不存在就先建桶。

一次探测的结论只有探明不认才是失败，其余都是"尚未确认"：

| 结论 | 错误码 | 启动时（命名实例：建出时） | 之后 | 怎么办 |
| --- | --- | --- | --- | --- |
| 服务不认条件写入（第二次没被拒，或回 501） | `OBJECTSTORAGE_UNAVAILABLE_003` | 启动失败（`KERNEL_HOSTING_002`，内层是本码）；命名实例不建、不登记，下次取用重新探测 | 后台或写入时才探明的：不覆盖写入一律返回本码，健康项 Failed，记 Error 日志，不再重试 | 换用认条件写入的服务（Amazon S3、RustFS、MinIO 实测通过）；应用从不写 `Overwrite = false` 时关闭 `ConditionalWrites`，重启 |
| 连不上、超时或其余服务端错误 | `OBJECTSTORAGE_UNAVAILABLE_001` | 照常启动、照常建出，尚未确认 | 后台重试，通过即恢复 | 检查端点与网络 |
| 被拒绝（403：密钥错、凭据只读、无权写探测键或放弃分片上传） | `OBJECTSTORAGE_DENIED_002` | 照常启动、照常建出，尚未确认 | 后台重试，权限改好即恢复 | 给凭据开探测键的写入与放弃分片上传权限；只读的应用关闭 `ConditionalWrites` |
| 探测桶不存在 | `OBJECTSTORAGE_NOTFOUND_001` | 照常启动、照常建出，尚未确认 | 后台重试，桶建好即恢复 | 先建桶、开 `EnsureBuckets`，或把 `HealthBucket` 改成已存在的桶 |

尚未确认时：

- 后台重试：第一次 1 秒后，之后间隔加倍，最长 `HealthProbeIntervalSeconds`（缺省 15 秒），按容器里的 `TimeProvider` 计时；有了结论即停，应用停止或命名实例释放时也停。
- `Overwrite = false` 的写入、分片上传的发起与完成：有探测在进行就等它；没有而距上一次探测满 1 秒，就地再探一次（每个实例同一时刻只有一个探测，后台重试与写入共用）；仍未确认返回 `OBJECTSTORAGE_UNAVAILABLE_004`（HTTP 503，参数 `reason` 是最近一次探测失败的错误码），不访问写入目标，不退回先查再写。覆盖写入、读取、删除、列举与预签名照常。
- 健康项 `objectstorage.s3.connection` 为 Degraded，错误是最近一次探测失败的错误码（即便 HEAD 成功），数据 `conditionalWrites` 为 `unconfirmed`；探测通过后即时恢复，不等下一轮健康探测。命名实例的健康项 `objectstorage.store.<name>` 同样 Degraded。

`ConditionalWrites = false` 只给明确不需要不覆盖写入的应用（只做覆盖写入，或凭据只读）：不探测，`HealthBucket` 可以不配；`Overwrite = false` 的写入、分片上传的发起与完成一律返回 `OBJECTSTORAGE_UNAVAILABLE_003`，不访问存储，框架不做没有保证的先查再写。覆盖写入、读取、删除、列举与预签名不受影响。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `S3ObjectStorageFeature` | 特性入口（`IHostedFeature`、`IShadowAwareFeature`；被顶替或其余能力只是兜底选出时只登记命名实例 Provider）；常量 `ConnectionBinding` = `objectstorage`、`HealthName` = `objectstorage.s3.connection`；`CreateClient(options)` 按选项建 `AmazonS3Client` |
| `S3BlobStore` | 实现 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner`；公开构造函数接收外部的 `IAmazonS3`，此时不拥有客户端 |
| `S3ObjectStorageOptions` | 选项；`FromConfiguration(configuration, binding)`、`FromSettings`（命名实例的设置，未知键抛出） |
| `S3BlobStoreProvider` | 命名实例的 Provider，`ProviderName` = `s3`；每个实例按自己的端点、区域与密钥建客户端 |

## 使用示例

本地用 S3 兼容的 RustFS 开发（MinIO 社区版已停发镜像，ADR-0086）：

```bash
docker run -p 9000:9000 -e RUSTFS_ACCESS_KEY=rustfsadmin -e RUSTFS_SECRET_KEY=rustfsadmin rustfs/rustfs:1.0.4
```

按租户使用不同的桶与密钥，定义来自应用自己的库：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;

var definition = new BlobStoreDefinition("tenant-42", "s3", new Dictionary<string, string>
{
    ["ServiceUrl"] = "https://oss-cn-hangzhou.aliyuncs.com",
    ["AccessKey"] = settings.AccessKey,
    ["SecretKey"] = settings.SecretKey,
});
var store = (await stores.GetOrAddAsync(definition, cancellationToken)).ValueOrThrow();

// S3 的命名实例同时是签名器，签出的地址直连该实例的端点
var signer = (IBlobUrlSigner)store;
var link = (await signer.SignAsync("invoices", "2026/10/inv-1.pdf", BlobUrlRequest.ForDownload(TimeSpan.FromMinutes(10)), cancellationToken)).ValueOrThrow();
```

`settings` 是应用保存的租户存储配置。

## 行为与限制

- 不覆盖写入：`Overwrite = false` 时先 HEAD，再带 `If-None-Match: *` 写入，由服务端保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`；服务认条件写入要先由探测确认（上文「条件写入探测」），探明之前返回 `OBJECTSTORAGE_UNAVAILABLE_004`，不认的服务（实测 Garage、Zenko CloudServer）启动时探明即启动失败、之后探明的不覆盖写入一律 `OBJECTSTORAGE_UNAVAILABLE_003`，不会带着可能静默覆盖的不覆盖写入运行。
- 不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。
- 读取：`GetAsync` 的内容流释放时连同响应一起释放；`HeadAsync` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页，续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为缺省值、自定义元数据为空，需要时再 `HeadAsync`；不符合键文法的对象（以 `/` 结尾的「目录」占位）跳过；桶不存在视作空容器。
- 范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长；起点越界（416）返回 `OBJECTSTORAGE_VALIDATION_001`。
- 分片上传直接映射 S3 multipart，`ListPartsAsync` 分页取全；不覆盖的发起与完成同样要求条件写入已确认（探测也核对分片的条件完成），完成先 HEAD 再带 `If-None-Match: *`；服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）返回 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 返回 `OBJECTSTORAGE_NOTFOUND_002`。
- 预签名经 SDK 在本地签出（签名 V4，端点是 http 时按 http 签）：下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号。
- 没有配额，由桶策略管理。调用方的取消原样抛出。
- 健康项 `objectstorage.s3.connection`：在启动时的条件写入探测之后启动，启动时同步探一次，之后按 `HealthProbeIntervalSeconds` 在后台重探并缓存结果（只做 HEAD 或列桶）；失败或超时为 Degraded（`KERNEL_UNAVAILABLE_005`，原因是 SDK 的异常消息），应用仍在服务。条件写入的状态即时并入：数据 `conditionalWrites` 为 `confirmed` / `unconfirmed` / `unsupported` / `disabled`，尚未确认 Degraded，探明不认 Failed（`OBJECTSTORAGE_UNAVAILABLE_003`）。
- 命名实例建出时探测条件写入（探明不认才不建；尚未确认的照常建出、后台重试；`ConditionalWrites` 关闭时不探测、建实例不发请求），被替换、移除或应用停止时停止重试、释放各自的客户端；实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；健康探测对实例的 `HealthBucket` 做 HEAD（没配时列桶），连通之后再看条件写入，尚未确认或不认时 Degraded。
- 通过 `ObjectStorage.Conformance` 全部用例（对 RustFS 1.0.4 验证，此前对 MinIO 验证；其余兼容服务的实测差异见 ADR-0086）。测试项目在设置 `XIHAN_TEST_S3`（如 `http://localhost:9000`）时对真实服务运行，`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `rustfsadmin`（RustFS 镜像不设密钥时的根凭据）；未设置时跳过。`XIHAN_TEST_S3_UNCONDITIONAL` 指向不认条件写入的兼容服务（如 Zenko CloudServer）时，另核对启动与命名实例的建出都以 `OBJECTSTORAGE_UNAVAILABLE_003` 失败。

| 错误码 | 含义 |
| --- | --- |
| `OBJECTSTORAGE_NOTFOUND_001` | 键或桶不存在（含条件写入探测的桶：尚未确认的原因之一） |
| `OBJECTSTORAGE_NOTFOUND_002` | 分片上传不存在（`NoSuchUpload`） |
| `OBJECTSTORAGE_VALIDATION_001` | 读取范围的起点越界 |
| `OBJECTSTORAGE_VALIDATION_002` | 服务端拒绝分片清单 |
| `OBJECTSTORAGE_CONFLICT_001` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | 其余服务端错误、网络错误与 SDK 超时（含探测连不上或超时：尚未确认的原因之一） |
| `OBJECTSTORAGE_UNAVAILABLE_003` | 服务不认条件写入（启动时探明即启动失败或不建命名实例，之后探明的不覆盖写入一律返回它），或 `ConditionalWrites = false` 时的不覆盖写入 |
| `OBJECTSTORAGE_UNAVAILABLE_004` | 条件写入尚未确认时的不覆盖写入（`reason` 是最近一次探测失败的错误码），探测通过后自动恢复 |
| `OBJECTSTORAGE_DENIED_002` | 条件写入探测被拒绝（403：尚未确认的原因之一） |

## 相关

- [对象存储](../guide/storage)
- [配置与选项](../guide/configuration)
- [ObjectStorage.Abstractions](./objectstorage-abstractions)
- [ObjectStorage.FileSystem](./objectstorage-filesystem)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050、ADR-0089
