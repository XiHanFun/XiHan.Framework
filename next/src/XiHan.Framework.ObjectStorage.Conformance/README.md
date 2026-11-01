# XiHan.Framework.ObjectStorage.Conformance

对象存储一致性套件（RFC-0012）。Provider 的测试项目继承 `BlobStoreConformance`、实现 `CreateStore()`，全部用例随之执行：读写往返与元数据、覆盖与不覆盖、并发的不覆盖写入只有一个成功（8 个写入方同时写同一个新键，ADR-0089）、键的区分大小写与嵌套段、批量写入的逐项结果、批量删除的逐项结果、按前缀分页列举、范围读取（`GetRange`）、分片上传（任意顺序上传、原子完成、非法分片列表、覆盖与中止）、非法标识提前拒绝、列举的页大小越界是校验错误（`OBJECTSTORAGE_VALIDATION_005`，1 与上限照常列举）、不存在的错误、大对象流式读写。

```csharp
public sealed class FileSystemBlobStoreConformanceTests : BlobStoreConformance
{
    protected override IBlobStore CreateStore() => new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = TempDirectory() }, TimeProvider.System);
}
```

命名实例的 Provider（ADR-0050）继承 `BlobStoreProviderConformance`，实现 `CreateProvider()` 与两组可用的设置：按定义建出的实例可读写且探测通过、未知的设置键被拒绝、经注册表同定义复用 / 定义变了替换 / 移除后不存在、定义错误翻译为 `OBJECTSTORAGE_VALIDATION_003` 且不影响已有实例。

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
