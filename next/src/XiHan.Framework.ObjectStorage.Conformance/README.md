# XiHan.Framework.ObjectStorage.Conformance

对象存储一致性套件（RFC-0012）。Provider 的测试项目继承 `BlobStoreConformance`、实现 `CreateStore()`，全部用例随之执行：读写往返与元数据、覆盖与不覆盖、批量删除的逐项结果、按前缀分页列举、不存在的错误、键的区分大小写与嵌套段、大对象流式读写。

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
