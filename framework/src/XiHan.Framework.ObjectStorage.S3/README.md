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

## 主要类型

此库提供的主要类型：

* `S3ObjectStorageFeature`：特性入口
* `S3ObjectStorageOptions`：端点、区域、密钥、探测桶与条件写入
* `S3BlobStore`：`IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner` 的实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/objectstorage-s3)

## 相关包

* 对象存储契约：[XiHan.Framework.ObjectStorage.Abstractions](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.Abstractions)
* 文件系统实现：[XiHan.Framework.ObjectStorage.FileSystem](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.FileSystem)

## 反馈与贡献

`XiHan.Framework.ObjectStorage.S3` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
