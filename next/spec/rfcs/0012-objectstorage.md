# RFC-0012 对象存储

> 状态：已采纳 | 日期：2026-10-23 | 对应 ADR：— | 参考实现：`XiHan.Framework.ObjectStorage.Abstractions`、`XiHan.Framework.ObjectStorage.FileSystem` | 一致性：`XiHan.Framework.ObjectStorage.Conformance`

## 摘要

对象存储子系统只定义一个字节流级的 blob 契约：容器里按键存放不可变的字节与元数据。写入与删除以批量为主（单条是批量的特例），读取按键，列举按前缀分页。Provider 只实现五个操作。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `objectstorage.blob@1` | 容器 / 键 / 字节 / 元数据的对象存储，`IBlobStore` | `ObjectStorage.FileSystem`（本地目录，带配额） |

## 标识

- 容器 `ContainerName`：`^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$`（与 S3 桶名兼容）。
- 键 `BlobKey`：`/` 分段的路径，1 到 1024 个字符；每段非空、不是 `.` 或 `..`；不含空白、控制字符与 `\`；区分大小写。
- 自定义元数据名：`^[a-z0-9][a-z0-9-]{0,63}$`；值为任意字符串。
- 标识不合法在构造时抛出 `ArgumentException`（编程错误），不进入 Provider。

## 契约

| 操作 | 语义 |
| --- | --- |
| `Put(container, uploads[])` | 按顺序逐项写入；每项带键、内容流与选项（内容类型、自定义元数据、是否允许覆盖）；返回逐项结果（元数据或错误）。空列表返回空；重复键按顺序各执行一次 |
| `Get(container, key)` | 打开内容流与元数据；不存在为 `OBJECTSTORAGE_NOTFOUND_001` |
| `Head(container, key)` | 只取元数据 |
| `Delete(container, keys[])` | 按顺序逐项删除；返回逐项结果（是否存在，或错误）；重复键第二次报告不存在 |
| `List(container, query)` | 按前缀列举，键的序数顺序，`PageSize`（1–1000，默认 1000）分页，`ContinuationToken` 不透明 |

- 元数据 `BlobMetadata`：键、长度、内容类型（默认 `application/octet-stream`）、`ETag`（内容变化即变化）、最后修改时间、自定义元数据。
- `Overwrite = false` 且已存在 → `OBJECTSTORAGE_CONFLICT_001`，不写入。
- 容器按需创建（文件系统）或由 Provider 的基础设施预先存在（S3 桶）；契约不管理容器。
- 基础设施失败（IO、网络）→ `OBJECTSTORAGE_UNAVAILABLE_001`；配额或单对象大小超限 → `OBJECTSTORAGE_UNAVAILABLE_002`，不写入。
- 批量操作在两项之间检查取消；已完成的项不回滚。
- 写入是原子的：读取方要么看到旧对象，要么看到完整的新对象。

单条便捷方法（`BlobStoreExtensions`）：`Put(container, key, stream | bytes, options)`、`Delete(container, key)`、`Exists`、`ReadAllBytes`，都转调批量方法。

## 默认实现

`ObjectStorage.FileSystem`：根目录（默认 `<AppContext.BaseDirectory>/blobs`）下 `<container>/data/<key>` 存内容、`<container>/meta/<key>.json` 存元数据；写入先落临时文件再原子替换；`ETag` 是内容的 SHA-256；配额 `MaxTotalBytes`（默认 1 GiB）与 `MaxBlobBytes`（默认 256 MiB），启动时扫描目录得到已用量，之后在进程内计量；单进程正确，多进程共享目录时配额只是近似。

## 一致性

Provider 必须继承 `BlobStoreConformance` 并通过全部用例。

## 错误码

见 `conventions/error-codes.md` 中 `OBJECTSTORAGE_*`。
