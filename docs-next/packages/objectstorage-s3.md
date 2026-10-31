# XiHan.Framework.ObjectStorage.S3

对象存储的 S3 Provider：容器即桶，键即对象键，经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等），同时提供原生分片上传与预签名地址。多实例部署、对象量大或需要客户端直连存储时使用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.ObjectStorage.S3` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `objectstorage.blob@1`、`objectstorage.multipart@1`、`objectstorage.presign@1`（`role: provider`）；依赖 `kernel.hosting@1`；可选绑定 `objectstorage`（`storage:object`） |
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

用本包时不要再装 `HostedBlobUrlFeature`：两者都提供 `objectstorage.presign@1`，S3 自己能签名。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServiceUrl` | 绝对 http / https 地址 | 空（Amazon S3） | 兼容服务的端点，如 `http://localhost:9000` |
| `Region` | `string` | `us-east-1` | 访问 Amazon S3 时选端点，访问兼容服务时作签名区域 |
| `AccessKey` | `string` | 空 | 访问密钥 ID；与 `SecretKey` 都为空时走 SDK 默认凭据链 |
| `SecretKey` | `string` | 空 | 访问密钥 |
| `ForcePathStyle` | `bool` | `true` | 桶名放在路径而不是主机名里；各兼容服务对两种寻址的支持不同，按服务商说明设置 |
| `EnsureBuckets` | `bool` | `false` | 写入遇到桶不存在时建桶再重试一次；缺省由基础设施预先建桶 |
| `HealthBucket` | `string` | 空 | 健康探测对它做 HEAD（同时验证凭据对该桶的权限）；为空时列桶 |
| `HealthProbeIntervalSeconds` | 正数 | 15 | 健康探测的间隔，也是单次探测的超时 |

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

绑定 `objectstorage` 存在时覆盖配置：主值是服务端点，属性 `region`、`accessKey`、`secretKey`、`forcePathStyle`。Kubernetes 里按 Service Binding 规范挂载的目录每个属性一个文件：

```text
/bindings/objectstorage/type          storage:object
/bindings/objectstorage/uri           http://minio:9000
/bindings/objectstorage/accessKey     dev-access-key
/bindings/objectstorage/secretKey     dev-secret-key
```

绑定的解析规则见[配置与选项](../guide/configuration#资源绑定)。命名实例（Provider 名 `s3`）的设置用同样的键，不含 `HealthProbeIntervalSeconds`（探测间隔由注册表统一），也不读绑定。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `S3ObjectStorageFeature` | 特性入口（`IHostedFeature`）；常量 `ConnectionBinding` = `objectstorage`、`HealthName` = `objectstorage.s3.connection`；`CreateClient(options)` 按选项建 `AmazonS3Client` |
| `S3BlobStore` | 实现 `IBlobStore`、`IBlobMultipartStore`、`IBlobUrlSigner`；公开构造函数接收外部的 `IAmazonS3`，此时不拥有客户端 |
| `S3ObjectStorageOptions` | 选项；`FromConfiguration(configuration, binding)`、`FromSettings`（命名实例的设置，未知键抛出） |
| `S3BlobStoreProvider` | 命名实例的 Provider，`ProviderName` = `s3`；每个实例按自己的端点、区域与密钥建客户端 |

## 使用示例

本地用 MinIO 开发：

```bash
docker run -p 9000:9000 quay.io/minio/minio server /data
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

- 不覆盖写入：`Overwrite = false` 时先 HEAD，再带 `If-None-Match: *` 写入；服务端支持条件写入（Amazon S3、MinIO）时由它保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`。
- 不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。
- 读取：`GetAsync` 的内容流释放时连同响应一起释放；`HeadAsync` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页，续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为缺省值、自定义元数据为空，需要时再 `HeadAsync`；不符合键文法的对象（以 `/` 结尾的「目录」占位）跳过；桶不存在视作空容器。
- 范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长；起点越界（416）返回 `OBJECTSTORAGE_VALIDATION_001`。
- 分片上传直接映射 S3 multipart，`ListPartsAsync` 分页取全；不覆盖的完成先 HEAD 再带 `If-None-Match: *`；服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）返回 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 返回 `OBJECTSTORAGE_NOTFOUND_002`。
- 预签名经 SDK 在本地签出（签名 V4，端点是 http 时按 http 签）：下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号。
- 没有配额，由桶策略管理。调用方的取消原样抛出。
- 健康项 `objectstorage.s3.connection`：启动时同步探一次，之后按 `HealthProbeIntervalSeconds` 在后台重探并缓存结果；失败或超时为 Degraded（`KERNEL_UNAVAILABLE_005`，原因是 SDK 的异常消息），应用仍在服务。
- 命名实例被替换、移除或应用停止时释放各自的客户端；实例同时是 `IBlobMultipartStore` 与 `IBlobUrlSigner`；探测对实例的 `HealthBucket` 做 HEAD，没配时列桶。
- 通过 `ObjectStorage.Conformance` 全部用例（对 MinIO 验证）。测试项目在设置 `XIHAN_TEST_S3`（如 `http://localhost:9000`）时对真实服务运行，`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `minioadmin`；未设置时跳过。

| 错误码 | 含义 |
| --- | --- |
| `OBJECTSTORAGE_NOTFOUND_001` | 键或桶不存在 |
| `OBJECTSTORAGE_NOTFOUND_002` | 分片上传不存在（`NoSuchUpload`） |
| `OBJECTSTORAGE_VALIDATION_001` | 读取范围的起点越界 |
| `OBJECTSTORAGE_VALIDATION_002` | 服务端拒绝分片清单 |
| `OBJECTSTORAGE_CONFLICT_001` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | 其余服务端错误、网络错误与 SDK 超时 |

## 相关

- [对象存储](../guide/storage)
- [配置与选项](../guide/configuration)
- [ObjectStorage.Abstractions](./objectstorage-abstractions)
- [ObjectStorage.FileSystem](./objectstorage-filesystem)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050
