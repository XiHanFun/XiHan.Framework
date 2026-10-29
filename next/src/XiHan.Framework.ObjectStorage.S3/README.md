# XiHan.Framework.ObjectStorage.S3

对象存储的 S3 Provider（Ring 3，`role: provider`）。提供 `objectstorage.blob@1`（RFC-0012）：容器即桶，键即对象键；经 AWS SDK 访问 Amazon S3 与兼容服务（MinIO、阿里云 OSS、腾讯云 COS、Cloudflare R2 等）。

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<S3ObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "S3": { "ServiceUrl": "http://localhost:9000", "Region": "us-east-1", "AccessKey": "minioadmin", "SecretKey": "minioadmin", "ForcePathStyle": true, "EnsureBuckets": false } } } }
```

绑定 `objectstorage`（`storage:object`，可选）存在时覆盖配置：主值是服务端点，属性 `region`、`accessKey`、`secretKey`、`forcePathStyle`。

## 规则

- `ServiceUrl` 为空时按 `Region` 访问 Amazon S3；有值时指向兼容服务，`Region` 作签名区域（默认 `us-east-1`），并只在服务端要求时计算 / 校验校验和（多数兼容服务不支持分块尾部校验和）。`AccessKey` / `SecretKey` 都为空时走 SDK 默认凭据链（环境变量、配置文件、实例角色）。应用自己注册的 `IAmazonS3` 优先于本特性创建的客户端。
- 写入：`Overwrite = false` 先 HEAD 再带 `If-None-Match: *` 写入，服务端支持条件写入（Amazon S3、MinIO）时由它保证原子，已存在返回 `OBJECTSTORAGE_CONFLICT_001`；不可定位的内容流先落临时文件（关闭即删）取得长度，不整体装进内存。`EnsureBuckets = true` 时桶不存在就建桶再重试一次，默认由基础设施预先建桶。
- 读取：`Get` 的内容流释放时连同响应一起释放；`Head` 返回长度、内容类型、ETag、修改时间与去掉 `x-amz-meta-` 前缀的自定义元数据。
- 批量删除：S3 的删除不报告对象是否存在过，先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键按每批 1000 个 `DeleteObjects`；删除失败只影响本次负责删除的项。
- 列举：`ListObjectsV2` 按前缀分页（S3 保证键的序数顺序），续页标记就是 S3 的 continuation token；条目只有键、长度、ETag 与修改时间，内容类型为默认值、自定义元数据为空，需要时再 `Head`；不符合键文法的对象（如以 `/` 结尾的"目录"占位）跳过；桶不存在视作空容器。
- 错误：不存在的键与桶映射为 `OBJECTSTORAGE_NOTFOUND_001`，其余服务端错误、网络错误与 SDK 超时映射为 `OBJECTSTORAGE_UNAVAILABLE_001`；调用方的取消原样抛出。没有配额（由桶策略管理）。
- 健康项 `objectstorage.s3.connection`：启动时同步探一次，之后按 `HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果；配了 `HealthBucket` 就对它 HEAD（桶不存在、无权限或服务不可达都算失败），没配就列桶；失败或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 是 SDK 的异常消息），应用仍在服务。
- 范围读取、分片与预签名（ADR-0049，同时提供 `objectstorage.multipart@1` 与 `objectstorage.presign@1`）：范围读取经 `Range` 头，`Content-Range` 给出实际范围与总长，起点越界（416）报 `OBJECTSTORAGE_VALIDATION_001`；分片直接映射 S3 multipart，`ListParts` 分页取全，不覆盖的完成先 HEAD 再带 `If-None-Match: *`，服务端拒绝分片清单（`InvalidPart`、`EntityTooSmall` 等）报 `OBJECTSTORAGE_VALIDATION_002`，`NoSuchUpload` 报 `OBJECTSTORAGE_NOTFOUND_002`；预签名经 SDK 本地签出（签名 V4，端点是 http 时按 http 签），下载可带 `Content-Disposition`，上传可限定内容类型，分片地址带上传标识与编号，客户端直连存储。
- 不满足 AOT 分析器（AWS SDK 的请求编组基于运行时反射）。

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions` 与 `AWSSDK.S3`。

## 测试

`ObjectStorage.S3.Tests` 在设置环境变量 `XIHAN_TEST_S3`（服务端点，如 `http://localhost:9000`）时对真实服务跑 `ObjectStorage.Conformance` 全部用例，`XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 缺省为 `minioadmin`；未设置时跳过。本地用 `docker run -p 9000:9000 quay.io/minio/minio server /data` 起一个 MinIO 即可。

## 状态

P5：可用，通过 `ObjectStorage.Conformance`（MinIO）。
