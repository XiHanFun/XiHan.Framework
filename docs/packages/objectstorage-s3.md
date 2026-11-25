# XiHan.Framework.ObjectStorage.S3

对象存储的 S3 Provider：容器即桶，键即对象键，经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等），同时提供原生分片上传与预签名地址。多实例部署、对象量大或需要客户端直连存储时使用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.ObjectStorage.S3` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `objectstorage.blob@1`、`objectstorage.multipart@1`、`objectstorage.presign@1`（`role: provider`）；依赖 `kernel.hosting@1`；可选绑定 `objectstorage`（`blob`） |
| 特性入口 | `S3ObjectStorageFeature` |
| 配置节 | `XiHan:ObjectStorage:S3` |
| 依赖 | `XiHan.Framework.ObjectStorage.Abstractions`；`AWSSDK.S3` |
| AOT | 不兼容（AWS SDK 的请求编组基于运行时反射） |
| 稳定性 | Stable；`S3BlobStoreProvider` 的构造依赖 Preview 的出站地址策略，为 Preview |

## 概述

本包是显式 Provider，装配后顶替默认的文件系统存储，三个能力都由同一个 `S3BlobStore` 实例提供。客户端由特性按选项创建，停止时释放；临时凭据的会话令牌、请求超时、尝试次数与 HTTP 代理也是选项（见「配置」）。选项仍然表达不了的（自定义凭据链等），应用在装配前向容器注册自己的 `IAmazonS3`，缺省实例沿用它，这时配置里的这几个键对它不起作用，应用也直接用到 AWS SDK 的类型，SDK 升大版本要跟着改。本包的公开类型不带 AWS SDK 的类型，`S3BlobStore` 没有公开构造：更多的实例经 `BlobStores` 按定义登记，各建各的客户端。各家兼容服务共用这一个包，不为每家云单独做包。

`ServiceUrl` 为空时按 `Region` 访问 Amazon S3；有值时指向兼容服务，`Region` 作签名区域，并只在服务端要求时计算与校验校验和（多数兼容服务不支持分块尾部校验和）。`AccessKey` 与 `SecretKey` 都为空时走 SDK 的默认凭据链（环境变量、配置文件、实例角色），只给其中一项是配置错误。

## 何时使用

- 生产环境、多实例部署、需要预签名直传与直下：用本包。
- 开发与单实例部署：默认的 [ObjectStorage.FileSystem](./objectstorage-filesystem) 即可。
- 应用要以 Native AOT 发布：本包不满足 AOT 分析器，改用文件系统存储，或应用实现自己的 `IBlobStore`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.ObjectStorage.S3 --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。

```csharp
builder.AddXiHan(xihan => xihan.UseFullProfile().UseFeature<S3ObjectStorageFeature>());
```

本包是缺省存储时不要再装 `HostedBlobUrlFeature`：两者都提供 `objectstorage.presign@1`，S3 自己能签名。缺省存储另选了文件系统、本包只为命名实例而装时，指定 `objectstorage.blob` = `xihan.objectstorage.filesystem` 即可：本包按被顶替处理，只登记 `s3` 命名实例 Provider，不读配置节与绑定、不建客户端、不探测。缺省的分片上传与预签名怎样指定、指定不一致时的 `OBJECTSTORAGE_VALIDATION_004` 见[命名存储实例](/object-storage/advanced#命名存储实例)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServiceUrl` | 绝对 http / https 地址 | 空（Amazon S3） | 兼容服务的端点，如 `http://localhost:9000` |
| `Region` | `string` | `us-east-1` | 访问 Amazon S3 时选端点，访问兼容服务时作签名区域 |
| `AccessKey` | `string` | 空 | 访问密钥 ID，必须与 `SecretKey` 同时给出或都不给；缺省实例两项都不给时走 SDK 默认凭据链（命名实例见下文）；与 `SecretKey` 一样在选项的 `ToString()` 里打印为 `***` |
| `SecretKey` | `string` | 空 | 访问密钥 |
| `SessionToken` | `string` | 空（没有令牌） | 临时凭据的会话令牌，必须与 `AccessKey`、`SecretKey` 同时给出；打印为 `***` |
| `RequestTimeoutSeconds` | 正数，可带小数，不超过 86400（`MaxRequestTimeout`） | 不另设时限 | 单次 HTTP 请求的超时，每次尝试各算各的；超时返回 `OBJECTSTORAGE_UNAVAILABLE_001`。下载的内容流交给调用方之后的读取不在其内 |
| `MaxAttempts` | 1 到 10（`MaxAttemptsLimit`） | SDK 的缺省（共 3 次） | 一次操作最多发出的请求数，含首次，1 即不重试；SDK 判定连接池里的连接已被对端关闭后重发的请求不计入（SDK 的 `MaxStaleConnectionRetries`） |
| `ProxyHost` | 主机名或 IP 字面量 | 空（不设代理） | HTTP 代理的主机，必须与 `ProxyPort` 同时给出；不带协议、端口、路径与用户信息，IPv6 字面量带不带方括号都收，不收带区域标识的 |
| `ProxyPort` | 1 到 65535 | — | HTTP 代理的端口 |
| `ProxyUserName` | `string` | 空（代理不带凭据） | 代理的用户名，必须与 `ProxyPassword` 同时给出，且只能跟着代理给；打印为 `***` |
| `ProxyPassword` | `string` | 空 | 代理的口令；打印为 `***` |
| `ForcePathStyle` | `bool` | `true` | 桶名放在路径而不是主机名里；各兼容服务对两种寻址的支持不同，按服务商说明设置 |
| `EnsureBuckets` | `bool` | `false` | 写入遇到桶不存在时建桶再重试一次；缺省由基础设施预先建桶 |
| `HealthBucket` | `string` | 空 | 框架探测用的桶：健康探测对它做 HEAD（同时验证凭据对该桶的权限）；`ConditionalWrites` 开启时在其中探测条件写入，这时必填；关闭且为空时健康探测列桶 |
| `ConditionalWrites` | `bool` | `true` | 不覆盖写入由条件写入（`If-None-Match: *`）保证：开启时探测服务是否支持，启动时探明不支持、凭据被拒或探测桶不存在即启动失败，只有连不上照常启动、后台重试；关闭表示应用不需要不覆盖写入，`Overwrite = false` 的写入一律失败（见下文「条件写入探测」） |
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

会话令牌、请求超时、尝试次数与代理这七个键都不写时，建出的客户端与 SDK 的缺省相同。字符串键空白不算给出；数值键写了就必须合法，空串也不行。成组的键只给了一部分（两项密钥只给一项、有令牌没有两项密钥、代理只有主机或只有端口、代理凭据只给一项或没有代理）、数值不在范围内都是配置错误，读完选项（绑定覆盖之后，两项密钥可以一项来自绑定、一项来自配置节）即核对：缺省实例启动失败，命名实例登记失败（`OBJECTSTORAGE_VALIDATION_003`）；手工构造的选项在建客户端时同样核对。报错带完整的键名，带出的原值只限数值与布尔值：两项密钥、令牌、代理的主机、用户名与口令以及服务端点（`ServiceUrl` 与绑定的主值）不合法时只报键名或绑定名，带用户信息的端点写错也不会把口令带进异常与日志。

绑定 `objectstorage` 存在时逐项覆盖配置：主值是服务端点，属性 `region`、`accessKey`、`secretKey`、`sessionToken`、`forcePathStyle`；空白的主值与属性不算给出，照读配置节。Kubernetes 里按 Service Binding 规范挂载的目录每个属性一个文件：

```text
/bindings/objectstorage/type          blob
/bindings/objectstorage/uri           http://s3.internal:9000
/bindings/objectstorage/accessKey     dev-access-key
/bindings/objectstorage/secretKey     dev-secret-key
```

绑定的解析规则见[配置与选项](/configuration/options)。命名实例（Provider 名 `s3`）的设置用同样的键，不含 `HealthProbeIntervalSeconds`（探测间隔由注册表统一），也不读绑定。

命名实例的客户端过[出站地址策略](/security/outbound)，定义可能来自应用的库，端点不可信；自建的 MinIO 等要放行。命名实例配了代理时（要运营方打开下文的 `NamedInstancesAllowProxy`），本进程建的连接是连代理的，过策略的是代理的地址：代理落在被拒绝的地址上与端点被拒绝同样处理（不建连接，读写返回 `OBJECTSTORAGE_UNAVAILABLE_001`）；端点由代理去解析、去连，本进程的策略管不到，要限制经代理能到的目标，在代理上做。没配代理的命名实例不用系统代理。缺省实例的端点与代理都是运维配的，不过策略。

命名实例必须给出 `AccessKey` 与 `SecretKey`，否则建实例失败（`OBJECTSTORAGE_VALIDATION_003`）；只给一项密钥总是拒绝。命名实例的设置可以由租户填写：没给密钥就会用宿主的 AWS 凭据链，填设置的人就能以宿主的身份访问它能访问的任何桶；配了代理时过出站地址策略的是代理的地址，策略放行了一个内网代理，填设置的人就能经它到达策略本来拒绝的地址。所以这两件事由运营方的开关管，开关只写在配置节 `XiHan:ObjectStorage:S3` 里，不在实例设置的键里（实例设置写了它们是未知的键），实例自己打不开；缺省实例不受它们约束：

| 键 | 缺省 | 打开之后 |
| --- | --- | --- |
| `NamedInstancesUseAmbientCredentials` | `false` | 命名实例可以不给两项密钥，用宿主的 AWS 凭据链；没打开时不给密钥即登记失败 |
| `NamedInstancesAllowProxy` | `false`（`S3BlobStoreProvider.AllowProxyKey`） | 命名实例可以在设置里配 `ProxyHost` 等四项；没打开时配了代理即登记失败（`OBJECTSTORAGE_VALIDATION_003`，原因写明要打开哪个键、不带代理的取值），不建客户端、不发任何连接 |

两项不是布尔值时，容器建 Provider（首次取用 `BlobStores`）即抛出 `ArgumentException`。

S3 原样存取内容类型：AWS SDK 会把 `Content-Type` 交给 .NET 的 `MediaTypeHeaderValue` 解析后重新写出，写出的与原值不逐字符相同的内容类型存不进原值。这类内容类型的写入与分片上传的发起返回 `OBJECTSTORAGE_VALIDATION_007`（HTTP 400，参数 `contentType`），先于不覆盖写入的 HEAD，一个请求都不发：

| 写法 | 结果 |
| --- | --- |
| `text/plain; charset=utf-8`、`application/pdf`、`Text/HTML`（.NET 的写法：`;` 之后一个空格） | 原样写入、原样读出 |
| `text/plain;`、`text/plain; charset=utf-8;`、`text/plain;;charset=utf-8`（空参数，SDK 解析不了） | `OBJECTSTORAGE_VALIDATION_007` |
| `text/plain;charset=utf-8`、`text/plain ; charset=utf-8`（`;` 两侧的空格与 `; ` 不同，浏览器常发 `text/plain;charset=UTF-8`） | `OBJECTSTORAGE_VALIDATION_007` |

要写进去就换成 .NET 的写法（`; ` 一个空格、去掉空参数），或拒绝这个请求；文件系统存储不受影响。预签名上传限定的内容类型由客户端自己发送，不经 SDK，不受此限。读取一侧有已知的缺口：经预签名地址由别的客户端写入的对象，SDK 解析得了而写法不同的内容类型按 .NET 的写法交出（存储里的 `text/plain;charset=utf-8` 读出 `text/plain; charset=utf-8`）。

## 条件写入探测

不覆盖写入（`Overwrite = false`）在 S3 上靠条件写入 `If-None-Match: *` 保证原子。有的兼容服务不认这个请求头，照常写入：先 HEAD 再写的两个写入方都会成功，后写的静默覆盖先写的（实测 Garage、Zenko CloudServer 上 8 个并发写入方全部"成功"）。所以本包在不覆盖写入之前先探明服务是否认它（ADR-0089、ADR-0090、ADR-0093）：

- 缺省实例在启动时探一次（与健康项的首次探测并行，S3 连不上时启动只等一个超时）；命名实例在建出时探一次，每个实例各探各的。
- 在 `HealthBucket` 里对探测键 `.xihan/probe/conditional-write` 带 `If-None-Match: *` 写两次，第二次必须被拒（412）；提供分片上传时再以同一个键带条件完成一次分片上传，也必须被拒，被拒的上传随即放弃。
- 探测键是空对象，写入后不删除：任何一次 412 都证明服务认条件写入，键已存在时第一次写入就被拒，所以多个实例同时启动、反复启动、后台重试都得到同一结论。凭据只需要对这个键的 `s3:PutObject` 与 `s3:AbortMultipartUpload`，不需要删除或列举。探测键在保留前缀 `.xihan` 下，不在键文法之内，业务列举跳过它（ADR-0090）。
- 单次探测以 `HealthProbeIntervalSeconds` 为超时；`EnsureBuckets` 开启时探测桶不存在就先建桶。

启动或建实例时，探明不认、被拒绝、探测桶不存在都是配置问题，要早暴露，启动失败；只有连不上是"尚未确认"，照常启动、后台重试（ADR-0093）：

| 结论 | 错误码 | 启动时（命名实例：建出时） | 之后 | 怎么办 |
| --- | --- | --- | --- | --- |
| 服务不认条件写入（第二次没被拒，或回 501） | `OBJECTSTORAGE_UNAVAILABLE_003` | 启动失败（`KERNEL_HOSTING_002`，内层是本码）；命名实例不建、不登记，下次取用重新探测 | 后台或写入时才探明的：不覆盖写入一律返回本码，健康项 Failed，记 Error 日志，不再重试 | 换用认条件写入的服务（Amazon S3、RustFS、MinIO 实测通过）；应用从不写 `Overwrite = false` 时关闭 `ConditionalWrites`，重启 |
| 被拒绝（403：密钥错、凭据只读、无权写探测键或放弃分片上传） | `OBJECTSTORAGE_DENIED_002` | 启动失败（`KERNEL_HOSTING_002`，内层是本码）；命名实例不建、不登记，下次取用重新探测 | 启动时连不上、连上时才被拒（凭据已被吊销，或配置本来就错）：仍是尚未确认、照常重试，健康项 Failed，记 Error 日志，权限改好即恢复 | 给凭据开探测键的写入与放弃分片上传权限；只读的应用关闭 `ConditionalWrites` |
| 探测桶不存在 | `OBJECTSTORAGE_NOTFOUND_001` | 启动失败（`KERNEL_HOSTING_002`，内层是本码）；命名实例不建、不登记 | 启动时连不上、连上时才发现缺桶：同上，桶建好即恢复 | 先建桶、开 `EnsureBuckets`，或把 `HealthBucket` 改成已存在的桶 |
| 连不上、超时或其余服务端错误（`reason` 只写异常类型，完整异常进日志） | `OBJECTSTORAGE_UNAVAILABLE_001` | 照常启动、照常建出，尚未确认 | 后台重试，通过即恢复（S3 晚于应用就绪、网络抖动） | 检查端点与网络 |

尚未确认时（启动时连不上的实例）：

- 后台重试：第一次 1 秒后，之后间隔加倍，最长 `HealthProbeIntervalSeconds`（缺省 15 秒），按容器里的 `TimeProvider` 计时；有了结论即停，应用停止或命名实例释放时也停。
- `Overwrite = false` 的写入、分片上传的发起与完成：有探测在进行就等它；没有而距上一次探测满 1 秒，就地再探一次（每个实例同一时刻只有一个探测，后台重试与写入共用）；仍未确认返回 `OBJECTSTORAGE_UNAVAILABLE_004`（HTTP 503，参数 `reason` 是最近一次探测失败的错误码），不访问写入目标，不退回先查再写。覆盖写入、读取、删除、列举与预签名照常。
- 健康项 `objectstorage.s3.connection` 的错误是最近一次探测失败的错误码（即便 HEAD 成功），数据 `conditionalWrites` 为 `unconfirmed`：连不上为 Degraded；连上后才被拒绝或缺桶为 Failed（配置错误要人处理），应用聚合为 Degraded、仍就绪，不覆盖写入仍是 `OBJECTSTORAGE_UNAVAILABLE_004`（`reason` 为 `OBJECTSTORAGE_DENIED_002` / `OBJECTSTORAGE_NOTFOUND_001`），不以 403 / 404 回给这次写入的调用方。探测通过后即时恢复，不等下一轮健康探测。命名实例的健康项 `objectstorage.store.<name>` 一律 Degraded（注册表的限制），错误同上。
- 启动之后才被拒绝或缺桶时继续重试、缺省实例健康项 Failed，是已定的做法（ADR-0097）：不改为停止重试（权限或桶修好之后还得重启），也不只记 Degraded（与连不上分不开，看不出要人处理）。
- 已确认之后凭据被吊销或桶被删除：已确认是终态，不再探测；读写照常访问存储、按错误映射失败（被拒等服务端错误为 `OBJECTSTORAGE_UNAVAILABLE_001`），连通性的 HEAD 失败让健康项 Degraded。

`ConditionalWrites = false` 只给明确不需要不覆盖写入的应用（只做覆盖写入，或凭据只读，或服务实测不认条件写入）：不探测，`HealthBucket` 可以不配；`Overwrite = false` 的写入、分片上传的发起与完成一律返回 `OBJECTSTORAGE_UNAVAILABLE_003`，不访问存储，框架不做没有保证的先查再写。覆盖写入、读取、删除、列举与预签名不受影响。关掉之后"只在不存在时写入"在这个存储上不再可用：需要它的业务改用不会冲突的键（如带随机后缀），或改为覆盖写入（`BlobPutOptions` 缺省就是覆盖写入）。

## 云厂商的兼容服务

阿里云 OSS、腾讯云 COS、Cloudflare R2 等兼容服务同样只用本包，框架不为各家的私有请求头做适配（OSS 的 `x-oss-forbid-overwrite`、COS 的 `x-cos-forbid-overwrite` 都不用），不覆盖写入只认标准的 `If-None-Match: *`（ADR-0090）。这几家是否认它框架没有实测，接入前要实测：

1. 直接用缺省的探测：把应用指向该服务，配好 `HealthBucket`。启动时探明不认即启动失败（`KERNEL_HOSTING_002`，内层 `OBJECTSTORAGE_UNAVAILABLE_003`；密钥或桶配错时内层是 `OBJECTSTORAGE_DENIED_002` / `OBJECTSTORAGE_NOTFOUND_001`）；探测通过时健康项 `objectstorage.s3.connection` 的数据 `conditionalWrites` 为 `confirmed`。
2. 或者手工发两次带条件的写入，第二次必须是 412（第一次在键已存在时也是 412，同样说明认）：

```bash
for i in 1 2; do
  curl -s -o /dev/null -w "%{http_code}\n" --aws-sigv4 "aws:amz:<区域>:s3" --user "<AccessKey>:<SecretKey>" \
    -X PUT -H "If-None-Match: *" --data-binary "" "https://<端点>/<桶>/.xihan/probe/conditional-write"
done
```

第二次返回 200 就是不认：把 `ConditionalWrites` 设为 `false`（命名实例的设置同名，可以只关那一个实例），并接受上一段写的后果。各家对路径式与虚拟主机式寻址的支持不同，按服务商的说明设置 `ForcePathStyle`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `S3ObjectStorageFeature` | 特性入口（`IHostedFeature`、`IShadowAwareFeature`；被顶替或其余能力只是兜底选出时只登记命名实例 Provider）；常量 `ConnectionBinding` = `objectstorage`、`HealthName` = `objectstorage.s3.connection` |
| `S3BlobStore` | 实现 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner`；没有公开构造：缺省实例由 `S3ObjectStorageFeature` 建出，命名实例由 `S3BlobStoreProvider` 按定义建出，都按「条件写入探测」探测 |
| `S3ObjectStorageOptions` | 选项；`FromConfiguration(configuration, binding)`、`FromSettings`（命名实例的设置，未知键抛出）；上限 `MaxRequestTimeout`（24 小时）与 `MaxAttemptsLimit`（10）；`ToString()` 把两项密钥、`SessionToken`、`ProxyUserName` 与 `ProxyPassword` 打印为 `***` |
| `S3BlobStoreProvider` | 命名实例的 Provider，`ProviderName` = `s3`；每个实例按自己的端点、区域与密钥建客户端；运营方开关的完整配置键 `AmbientCredentialsKey`、`AllowProxyKey`，由带 `IConfiguration` 的构造读取，不带配置的构造按两项都关闭 |

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
var link = (await signer.SignAsync(new ContainerName("invoices"), new BlobKey("2026/10/inv-1.pdf"), BlobUrlRequest.ForDownload(TimeSpan.FromMinutes(10)), cancellationToken)).ValueOrThrow();
```

`settings` 是应用保存的租户存储配置。

## 行为与限制

- 不覆盖写入：`Overwrite = false` 时先 HEAD，再带 `If-None-Match: *` 写入，由服务端保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`；服务认条件写入要先由探测确认，探明之前与探明不认时的结果见上文「条件写入探测」，不会带着可能静默覆盖的不覆盖写入运行。
- 不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。
- 读取：`GetAsync` 的内容流释放时连同响应一起释放；`HeadAsync` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页，续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为缺省值、自定义元数据为空，需要时再 `HeadAsync`；不符合键文法的对象（以 `/` 结尾的「目录」占位、保留前缀 `.xihan` 下的框架对象如探测键）跳过，所以一页可能少于 `PageSize` 条、甚至为空而仍带续页标记，取到续页标记为空为止；桶不存在视作空容器。
- 范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长；起点越界（416）返回 `OBJECTSTORAGE_VALIDATION_001`。
- 分片上传直接映射 S3 multipart，`ListPartsAsync` 分页取全；不覆盖的发起与完成同样要求条件写入已确认（探测也核对分片的条件完成），完成先 HEAD 再带 `If-None-Match: *`；服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）返回 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 返回 `OBJECTSTORAGE_NOTFOUND_002`。批量放弃没有原生接口，逐项请求 `AbortMultipartUpload`，上传或桶不存在都算成功。
- 预签名经 SDK 在本地签出（签名 V4，端点是 http 时按 http 签）：下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号。
- 没有配额，由桶策略管理。调用方的取消原样抛出。
- 健康项 `objectstorage.s3.connection`：启动时与条件写入探测并行同步探一次，之后按 `HealthProbeIntervalSeconds` 在后台重探并缓存结果（只做 HEAD 或列桶）；失败或超时为 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 只写异常类型，完整异常进日志），应用仍在服务。条件写入的状态即时并入：数据 `conditionalWrites` 为 `confirmed` / `unconfirmed` / `unsupported` / `disabled`，连不上 Degraded，启动之后才被拒绝或缺桶 Failed（`OBJECTSTORAGE_DENIED_002` / `OBJECTSTORAGE_NOTFOUND_001`），探明不认 Failed（`OBJECTSTORAGE_UNAVAILABLE_003`）。
- 命名实例建出时探测条件写入（探明不认、被拒绝、探测桶不存在都不建；只有连不上的照常建出、后台重试；`ConditionalWrites` 关闭时不探测、建实例不发请求），被替换、移除或应用停止时停止重试、释放各自的客户端；实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；健康探测对实例的 `HealthBucket` 做 HEAD（没配时列桶），连通之后再看条件写入，尚未确认或不认时 Degraded。
- 通过 `ObjectStorage.Conformance` 全部用例（对 RustFS 1.0.4 验证，也对 MinIO 验证过；其余兼容服务的实测差异见 ADR-0086）。测试项目在设置 `XIHAN_TEST_S3`（如 `http://localhost:9000`）时对真实服务运行，`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `rustfsadmin`（RustFS 镜像不设密钥时的根凭据）；未设置时跳过。`XIHAN_TEST_S3_UNCONDITIONAL` 指向不认条件写入的兼容服务（如 Zenko CloudServer）时，另核对启动与命名实例的建出都以 `OBJECTSTORAGE_UNAVAILABLE_003` 失败（2026-11-02 在 Zenko CloudServer 8.2.7 上补跑通过，ADR-0093）；对 RustFS 另核对密钥错、探测桶不存在让启动与命名实例的建出失败。

错误码的含义见 [ObjectStorage.Abstractions](/packages/objectstorage-abstractions#错误码)，本包特有的映射：

- `OBJECTSTORAGE_NOTFOUND_001`：键不存在；桶不存在时读取也是它，条件写入探测的桶不存在同样报它。
- `OBJECTSTORAGE_UNAVAILABLE_001`：其余服务端错误、网络错误与 SDK 超时（含 `RequestTimeoutSeconds` 到点）；探测连不上或超时也是它，是唯一照常启动、后台重试的原因。
- `OBJECTSTORAGE_UNAVAILABLE_003`、`OBJECTSTORAGE_UNAVAILABLE_004`、`OBJECTSTORAGE_DENIED_002`：只出自条件写入，何时返回见上文「条件写入探测」。
- `OBJECTSTORAGE_VALIDATION_007`：内容类型合文法、但 SDK 写不出原值，写入与分片上传的发起在发请求之前返回，见上文「配置」末尾的表。

## 相关

- [对象存储](/object-storage/basics)
- [配置与选项](/configuration/options)
- [ObjectStorage.Abstractions](./objectstorage-abstractions)
- [ObjectStorage.FileSystem](./objectstorage-filesystem)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050、ADR-0089
