# XiHan.Framework.ObjectStorage.S3

## 关于

`XiHan.Framework.ObjectStorage.S3` 是对象存储的 S3 提供程序：容器即桶，键即对象键，经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等），适用于多实例部署与客户端直连。

## 主要特性

* 读写、范围读取、分片上传、预签名地址，客户端可直连存储
* 不覆盖写入依赖服务端的条件写入，启动时探测服务是否支持
* 连通性健康项与命名实例

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.ObjectStorage.S3 --prerelease
```

### 使用

注册后顶替文件系统实现：

```csharp
builder.AddXiHan(xihan => xihan.UseFullProfile().UseFeature<S3ObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "S3": { "ServiceUrl": "http://localhost:9000", "Region": "us-east-1", "AccessKey": "<access-key>", "SecretKey": "<secret-key>", "ForcePathStyle": true, "HealthBucket": "uploads" } } } }
```

`ServiceUrl` 为空时访问 Amazon S3；`AccessKey` 与 `SecretKey` 都为空时走 SDK 默认凭据链，只给一项是配置错误（绑定的 `accessKey`、`secretKey` 覆盖之后再核对）。`ServiceUrl` 与绑定的主值不能带用户信息（`http://user:pass@host`、`http://user@host`，空的 `@` 也算），凭据只写在两项密钥（与绑定的同名属性）里；带了是配置错误，报错只写键名或绑定名。

内容类型原样存取：AWS SDK 会把 `Content-Type` 按 .NET 的写法重写，写不出原值的（`text/plain;` 这类空参数，`text/plain;charset=utf-8` 这类 `;` 两侧的空格与 `; ` 不同的，`text/plain;  charset=utf-8` 这类多了连续空格的）写入与分片上传的发起返回 `OBJECTSTORAGE_VALIDATION_007`，不发请求：不拒绝的话，空参数让 SDK 抛出，空格写法不同的验签失败，多了连续空格的验签通过、服务悄悄存下改写后的值（RustFS 实测）；要写进去就换成 .NET 的写法（`text/plain; charset=utf-8`）。读取时 SDK 同样按这个写法交出它解析得了的值。本包是缺省存储时不要再装 `HostedBlobUrlFeature`。

自定义元数据的值原样存取：S3 的用户元数据走 `x-amz-meta-*` 头，头里写不出非 ASCII 字符，首尾的空格也会被去掉。值里有非 ASCII 字符、以空格开头或结尾、或以 `=?` 开头且以 `?=` 结尾（RustFS 会把原样写出的这种值当作 encoded-word 去解，解不了就拒收）时，本包照 RFC 2047 把它编成 `=?UTF-8?B?…?=`（长的拆成几个、以空格隔开），读回时解码；其余的值原样写。整个是 encoded-word 形状的只有 ASCII 的值（如 `=?UTF-8?B?5Lit?=`）存不成原样：Amazon S3 按文档先解码再存，读回时又会被当作 encoded-word 解码，所以写入与分片上传的发起返回 `OBJECTSTORAGE_VALIDATION_009`（参数 `name`），不发请求；只是以 `=?` 开头、以 `?=` 结尾而不是这个形状的（如 `=?x?=`）照常编码写入。读回时认 B 与 Q 两种编码、UTF-8 字符集，别的写入方编好的 encoded-word 照样解开；别的写入方不编码、直接把 UTF-8 字节写进头的值，服务原样返回时 .NET 的 HTTP 客户端把每个字节读成一个 Latin-1 字符，本包还原成字节再严格按 UTF-8 解出（RustFS 读回时自己编成 encoded-word，同样解得开）。解不了的（别的字符集、坏的 base64、不是合法 UTF-8 的字节、解出控制字符等）当作服务端错误返回 `OBJECTSTORAGE_UNAVAILABLE_001`，不交出编码的文本或乱码。不用 SDK 的进程级开关 `AWSConfigsS3.EnableUnicodeEncodingForObjectMetadata`。RustFS 原样存下编码之后的文本；Amazon S3 按文档自己解码、编码，没有实测，它的 2 KB 元数据上限按编码之后的头计，非 ASCII 的字节多、接近 2048 字节上限的值会被它以 `MetadataTooLarge` 拒收，本包返回 `OBJECTSTORAGE_VALIDATION_008`（校验错误，不是存储故障），不预先按编码之后的长度拒收。

服务应答成功却缺了必需的内容，一律当作服务端错误返回 `OBJECTSTORAGE_UNAVAILABLE_001`，不抛异常，也不拿空串、最小时刻、0 顶替：读取与 `Head` 的应答没有 `ETag`、修改时间或 `Content-Length`，写入的应答没有 `ETag`，列举说截断了却没有新的续页标记、列出的对象缺长度，列出分片的分页不前进或同一编号出现两次，批量删除的错误项认不出是哪个键（这一批里要删的项都报错；认得出的只报那一项，`reason` 是 S3 的错误码，错误项没有错误码时是固定的 `DeleteObjects returned an error without a usable Code`；AWS SDK 遇到有错误项的应答抛出 `DeleteObjectsException`，本包从它带的应答里逐项报告），列出分片交出不比这一页起点大的编号，分片上传的发起没有上传标识。应答里的值 SDK 解析不了（`Last-Modified` 不是日期、`Content-Length` 或 `x-amz-mp-parts-count` 不是数等）同样如此，哪一步都一样（含放弃分片上传与 `EnsureBuckets` 的建桶），不再把 SDK 的 `FormatException` 抛给调用方；响应体不是良构的 XML 也一样，不再抛 `XmlException`。`EnsureBuckets` 的建桶失败同其余服务端错误。整读（不带范围的读取）的应答里 `ETag` 不是十六进制同样如此：AWS SDK 把它当作内容的 MD5，解码不了就在交出应答之前抛出；范围读取、`Head` 与写入不做这一核对，照常交出。`ETag` 是十六进制却不是内容的 MD5（有的兼容服务这样给，又没有 SSE 的应答头）时，SDK 读到内容末尾才发现：读内容的流在读到末尾（读返回 0）的那次读里抛出携带 `OBJECTSTORAGE_UNAVAILABLE_001` 的 `FrameworkException`，已经读到的字节要丢弃；没读到末尾就释放流（例如只 `ReadExactly` 声明的长度）时释放不抛出，只记一条 Warning，要这个报告就读到返回 0（`ReadToEnd`、`CopyTo`）。条件写入探测遇到发起分片上传的应答缺上传标识时，服务可能已经建了上传而探测放弃不了：这种应答的重试从 `HealthProbeIntervalSeconds` 起加倍、最长 1 小时，其间写入不就地探测，累计 10 次之后不再探测（健康项 Failed，修好服务或代理之后重启）。给 `HealthBucket` 配放弃未完成分片上传的生命周期规则（如 Amazon S3 的 `AbortIncompleteMultipartUpload`），留下的上传才会被清掉。写入结果的修改时间是本机收到应答时的时钟（S3 的写入应答本来不带它），之后读出的是服务记下的时刻；要服务端的时间就在写入之后再 `Head`。服务不支持条件写入时可设 `ConditionalWrites = false`，后果见概念文档。

端点、区域、密钥与路径风格都在配置里，临时凭据、超时、重试与代理也是。下面这几个键都是可选的，不写时客户端与 SDK 的缺省相同；配置节 `XiHan:ObjectStorage:S3` 与命名实例的设置里都能写：

| 键 | 取值 | 含义 |
| --- | --- | --- |
| `SessionToken` | 字符串 | 临时凭据的会话令牌，必须与 `AccessKey`、`SecretKey` 同时给出；绑定属性 `sessionToken` 覆盖它 |
| `RequestTimeoutSeconds` | 正数，不超过 86400 | 单次 HTTP 请求的超时，每次尝试各算各的；不写时不另设时限。超时返回 `OBJECTSTORAGE_UNAVAILABLE_001` |
| `MaxAttempts` | 1 到 10 | 一次操作最多发出的请求数，含首次，1 即不重试；SDK 判定连接池里的连接已被对端关闭后重发的请求不计入（SDK 的 `MaxStaleConnectionRetries`）；不写时取 SDK 的缺省（共 3 次） |
| `ProxyHost`、`ProxyPort` | 主机名或 IP 字面量；1 到 65535 | HTTP 代理，两项必须同时给出。主机不带协议、端口、路径与用户信息 |
| `ProxyUserName`、`ProxyPassword` | 字符串 | 代理的凭据，可选，两项必须同时给出，且只能跟着代理给 |

成组的键只给了一部分、数值不在范围内都是配置错误：缺省实例启动失败，命名实例登记失败（`OBJECTSTORAGE_VALIDATION_003`）。`SessionToken`、`ProxyUserName`、`ProxyPassword` 与两项密钥一样是凭据，打印选项时遮蔽，报错里不带它们的值；`ServiceUrl` 不合法时报错同样只写键名。命名实例的客户端过出站地址策略：配了代理时过策略的是代理的地址，端点由代理去连，要限制经代理能到的目标就在代理上做。

命名实例（经 `BlobStores` 按定义登记）的设置可能由租户填写，下面两项只由运营方写在配置节 `XiHan:ObjectStorage:S3` 里，不在实例设置的键里，实例自己打不开；缺省实例不受它们约束：

| 键 | 缺省 | 打开之后 |
| --- | --- | --- |
| `NamedInstancesUseAmbientCredentials` | `false` | 命名实例可以不给两项密钥，用宿主的 AWS 凭据链；没打开时不给密钥即登记失败 |
| `NamedInstancesAllowProxy` | `false` | 命名实例可以在设置里配 `ProxyHost` 等四项；没打开时配了代理即登记失败（原因不带代理的取值）。策略放行了一个内网代理时，能填实例设置的人就能经它到达策略本来拒绝的地址，打开前先想清楚 |

两项不是布尔值时，容器建 Provider（首次取用 `BlobStores`）即抛出。

选项仍然表达不了的（自定义凭据链等），在装配前向容器注册自己的 `IAmazonS3`，缺省实例沿用它，上面这几个键对它不起作用；这时应用直接用到 AWS SDK 的类型，SDK 升大版本要跟着改。本包的公开类型不带 SDK 的类型，`S3BlobStore` 没有公开构造：缺省实例由特性建出，更多实例经 `BlobStores` 按定义登记。

## 主要类型

此库提供的主要类型：

* `S3ObjectStorageFeature`：特性入口
* `S3ObjectStorageOptions`：端点、区域、密钥与会话令牌、探测桶与条件写入、请求超时、尝试次数与代理
* `S3BlobStore`：`IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 的实现，由特性或 `S3BlobStoreProvider` 建出
* `S3BlobStoreProvider`：命名实例的提供程序（名 `s3`）

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/objectstorage-s3)

## 相关包

* 对象存储契约：[XiHan.Framework.ObjectStorage.Abstractions](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.Abstractions)
* 文件系统实现：[XiHan.Framework.ObjectStorage.FileSystem](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.FileSystem)

## 反馈与贡献

`XiHan.Framework.ObjectStorage.S3` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
