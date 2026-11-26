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

服务应答成功却缺了必需的内容，一律当作服务端错误返回 `OBJECTSTORAGE_UNAVAILABLE_001`，不抛异常，也不拿空串、最小时刻顶替：读取与 `Head` 的应答没有 `ETag` 或修改时间，写入的应答没有 `ETag`，列举说截断了却没有新的续页标记，列出分片的分页不前进或超过 10000 片，批量删除的错误项认不出是哪个键（这一批里要删的项都报错），分片上传的发起没有上传标识。整读（不带范围的读取）的应答里 `ETag` 不是十六进制同样如此：AWS SDK 把它当作内容的 MD5，解码不了就在交出应答之前抛出；范围读取、`Head` 与写入不做这一核对，照常交出。条件写入探测遇到发起分片上传的应答缺上传标识时，服务可能已经建了上传而探测放弃不了：这种应答的重试从 `HealthProbeIntervalSeconds` 起加倍、最长 1 小时，其间写入不就地探测，累计 10 次之后不再探测（健康项 Failed，修好服务或代理之后重启）。给 `HealthBucket` 配放弃未完成分片上传的生命周期规则（如 Amazon S3 的 `AbortIncompleteMultipartUpload`），留下的上传才会被清掉。写入结果的修改时间是本机收到应答时的时钟（S3 的写入应答本来不带它），之后读出的是服务记下的时刻。服务不支持条件写入时可设 `ConditionalWrites = false`，后果见概念文档。

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
