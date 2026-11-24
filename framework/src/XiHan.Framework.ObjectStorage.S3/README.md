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

`ServiceUrl` 为空时访问 Amazon S3；`AccessKey` 与 `SecretKey` 都为空时走 SDK 默认凭据链。本包是缺省存储时不要再装 `HostedBlobUrlFeature`。服务不支持条件写入时可设 `ConditionalWrites = false`，后果见概念文档。

端点、区域、密钥与路径风格都在配置里，临时凭据、超时、重试与代理也是。下面这几个键都是可选的，不写时客户端与 SDK 的缺省相同；配置节 `XiHan:ObjectStorage:S3` 与命名实例的设置里都能写：

| 键 | 取值 | 含义 |
| --- | --- | --- |
| `SessionToken` | 字符串 | 临时凭据的会话令牌，必须与 `AccessKey`、`SecretKey` 同时给出；绑定属性 `sessionToken` 覆盖它 |
| `RequestTimeoutSeconds` | 正数，不超过 86400 | 单次 HTTP 请求的超时，每次尝试各算各的；不写时不另设时限。超时返回 `OBJECTSTORAGE_UNAVAILABLE_001` |
| `MaxAttempts` | 1 到 10 | 一次操作最多发出的请求数，含首次，1 即不重试；SDK 判定连接池里的连接已被对端关闭后重发的请求不计入（SDK 的 `MaxStaleConnectionRetries`）；不写时取 SDK 的缺省（共 3 次） |
| `ProxyHost`、`ProxyPort` | 主机名或 IP 字面量；1 到 65535 | HTTP 代理，两项必须同时给出。主机不带协议、端口、路径与用户信息 |
| `ProxyUserName`、`ProxyPassword` | 字符串 | 代理的凭据，可选，两项必须同时给出，且只能跟着代理给 |

成组的键只给了一部分、数值不在范围内都是配置错误：缺省实例启动失败，命名实例登记失败（`OBJECTSTORAGE_VALIDATION_003`）。`SessionToken`、`ProxyUserName`、`ProxyPassword` 与两项密钥一样是凭据，打印选项时遮蔽，报错里不带它们的值。命名实例的客户端过出站地址策略：配了代理时过策略的是代理的地址，端点由代理去连，要限制经代理能到的目标就在代理上做。

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
