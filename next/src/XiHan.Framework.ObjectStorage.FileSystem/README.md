# XiHan.Framework.ObjectStorage.FileSystem

文件系统对象存储（Ring 3，默认实现，`role: default`）。提供 `objectstorage.blob@1`（RFC-0012）。

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<FileSystemObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "FileSystem": { "RootPath": "/var/lib/app/blobs", "MaxTotalBytes": 1073741824, "MaxBlobBytes": 268435456 } } } }
```

## 规则

- 布局：`<RootPath>/<container>/data/<key>` 存内容，`<RootPath>/<container>/meta/<key>.json` 存元数据；根目录默认 `<AppContext.BaseDirectory>/blobs`；容器目录按需创建。
- 写入先落同目录的临时文件（名含 `.xihan-tmp-`），复制完成后原子替换，再写元数据；读取方要么看到旧对象要么看到完整的新对象。`ETag` 是内容的 SHA-256。
- 配额：`MaxTotalBytes`（默认 1 GiB）与 `MaxBlobBytes`（默认 256 MiB），复制过程中逐段检查，超限的项返回 `OBJECTSTORAGE_UNAVAILABLE_002` 且不留下内容。启动时扫描目录得到已用量并清理残留临时文件，之后在进程内计量；多进程共享同一目录时配额只是近似。
- 列举：遍历容器目录、序数排序、按前缀过滤，续页标记是上一页最后一个键；元数据缺失（外部直接放进目录的文件）按文件信息补一份。
- 健康项 `objectstorage.filesystem.capacity`（存储自己实现，同步计算）：已用量达到 `MaxTotalBytes` 即 Degraded（`OBJECTSTORAGE_UNAVAILABLE_002`，消息键 `objectstorage.quota.reached`），否则 Ready。

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions`。

## 状态

P3：可用，通过 `ObjectStorage.Conformance`。
