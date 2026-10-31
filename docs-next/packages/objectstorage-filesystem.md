# XiHan.Framework.ObjectStorage.FileSystem

对象存储的默认实现：本地目录按容器与键存放内容与元数据，原子替换写入，`ETag` 是内容的 SHA-256，带总量与单对象配额。适用于开发、测试、单实例部署，以及挂了共享卷的小规模部署。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.ObjectStorage.FileSystem` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `objectstorage.blob@1`、`objectstorage.multipart@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `FileSystemObjectStorageFeature` |
| 配置节 | `XiHan:ObjectStorage:FileSystem` |
| 依赖 | `XiHan.Framework.ObjectStorage.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

存储把每个容器放在根目录下的一个子目录里：

```text
<RootPath>/<container>/data/<key>          内容
<RootPath>/<container>/meta/<key>.json     元数据（内容类型、ETag、修改时间、自定义元数据）
<RootPath>/<container>/uploads/<上传标识>/  未完成的分片上传
```

键的各段经编码后作为路径，大小写不敏感的文件系统上键仍区分大小写；容器目录按需创建。写入先落同目录的临时文件（名字含 `.xihan-tmp-`），复制完成后原子替换，再写元数据。配额在进程内计量：启动时扫描目录得到已用量并清理残留的临时文件，之后每次写入与删除更新。

预签名不是文件系统的能力。需要限时地址时装 `Hosting.AspNetCore` 的 `HostedBlobUrlFeature` 并 `MapXiHanBlobs()`，由宿主代收发，见[对象存储](../guide/storage#预签名地址)。

## 何时使用

- 开发与测试，或单实例部署：默认就用它，`Profiles.Full` 已装配。
- 多实例部署、对象量大、需要客户端直连存储：用 [ObjectStorage.S3](./objectstorage-s3)。多个进程共享同一目录时本包的配额只是近似。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.ObjectStorage.FileSystem" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<FileSystemObjectStorageFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `RootPath` | `string` | `<AppContext.BaseDirectory>/blobs` | 根目录；相对路径按当前目录解析 |
| `MaxTotalBytes` | 正整数 | 1073741824（1 GiB） | 全部对象的总字节上限 |
| `MaxBlobBytes` | 正整数 | 268435456（256 MiB） | 单个对象的字节上限，分片与拼接后的对象同样受它约束 |
| `MultipartExpiryHours` | 正数 | 168（7 天） | 未完成的分片上传保留多久，启动扫描时清掉更早发起的 |

```json
{
  "XiHan": {
    "ObjectStorage": {
      "FileSystem": { "RootPath": "/var/lib/app/blobs", "MaxTotalBytes": 1073741824, "MaxBlobBytes": 268435456 }
    }
  }
}
```

非 root 运行的容器镜像里应用目录可能不可写，KubernetesApp 示例在生产配置里把 `RootPath` 设到 `/tmp/xihan-blobs`；要持久保存的数据挂卷并把 `RootPath` 指过去。

命名实例（Provider 名 `filesystem`）的设置用同样的四个键，`RootPath` 必填：

```json
{ "XiHan": { "ObjectStorage": { "Stores": { "archive": { "Provider": "filesystem", "RootPath": "/var/lib/app/archive" } } } } }
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `FileSystemObjectStorageFeature` | 特性入口（`IHostedFeature`）；启动时扫描目录；常量 `HealthName` = `objectstorage.filesystem.capacity` |
| `FileSystemBlobStore` | 实现 `IBlobStore` 与 `IBlobMultipartStore`；`RootPath`、`UsedBytes`；不经宿主使用时先调用 `InitializeAsync` |
| `FileSystemObjectStorageOptions` | 选项；`FromConfiguration`、`FromSettings`（命名实例的设置，未知键抛出） |
| `FileSystemBlobStoreProvider` | 命名实例的 Provider，`ProviderName` = `filesystem` |

## 使用示例

不经宿主直接使用（测试、命令行工具）：

```csharp
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.FileSystem;

var store = new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = "./data/blobs", MaxTotalBytes = 64L << 20 }, TimeProvider.System);
// 扫描已用量、清理残留的临时文件与过期的分片上传
await store.InitializeAsync(CancellationToken.None);

var put = await store.PutAsync("reports", "2026/10/summary.txt", "hello"u8.ToArray(), BlobPutOptions.Default, CancellationToken.None);
Console.WriteLine($"{put.ValueOrThrow().ETag}, used {store.UsedBytes} bytes");
```

## 行为与限制

- 原子性：读取方要么看到旧对象，要么看到完整的新对象。
- 配额：复制过程中逐段检查，超过 `MaxBlobBytes` 或 `MaxTotalBytes` 的项返回 `OBJECTSTORAGE_UNAVAILABLE_002`，不留下内容。单进程计量正确，多进程共享目录时只是近似。
- 列举：遍历容器目录、按序数排序、按前缀过滤，续页标记是上一页最后一个键；外部直接放进 `data/` 目录、没有元数据的文件按文件信息补一份元数据。
- 范围读取：打开内容文件后定位到起点，只交出该段。
- 分片上传：每次上传一个目录，`manifest.json` 记键与写入选项，分片文件名带编号与内容的 SHA-256，同一编号重传先落新文件再删旧的；完成时按清单拼接成同目录临时文件再原子替换，计入配额后删掉上传目录。暂存的分片不计入配额。上传标识只认本实现发出的 32 位小写十六进制。
- 健康项 `objectstorage.filesystem.capacity` 由存储同步计算：已用量达到 `MaxTotalBytes` 即 Degraded（`OBJECTSTORAGE_UNAVAILABLE_002`），否则 Ready。命名实例的探测用同一口径。
- 命名实例建出时即建目录并扫描用量，预签名用宿主签名器的 `ForStore(name)`。
- 通过 `ObjectStorage.Conformance` 的 `BlobStoreConformance` 与 `BlobStoreProviderConformance`。

| 错误码 | 含义 |
| --- | --- |
| `OBJECTSTORAGE_NOTFOUND_001` | 对象不存在 |
| `OBJECTSTORAGE_NOTFOUND_002` | 分片上传不存在或已结束 |
| `OBJECTSTORAGE_VALIDATION_001` | 读取范围的起点不在对象内 |
| `OBJECTSTORAGE_VALIDATION_002` | 分片清单不合法 |
| `OBJECTSTORAGE_CONFLICT_001` | 对象已存在且不允许覆盖 |
| `OBJECTSTORAGE_UNAVAILABLE_001` | IO 失败 |
| `OBJECTSTORAGE_UNAVAILABLE_002` | 配额或单对象大小超限 |

## 相关

- [对象存储](../guide/storage)
- [ObjectStorage.Abstractions](./objectstorage-abstractions)
- [ObjectStorage.S3](./objectstorage-s3)
- [Hosting.AspNetCore](./hosting-aspnetcore)
- 规范：RFC-0012《对象存储》、ADR-0049、ADR-0050
