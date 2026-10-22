# XiHan.Framework.ObjectStorage.Conformance

对象存储一致性套件（RFC-0012）。Provider 的测试项目继承 `BlobStoreConformance`、实现 `CreateStore()`，全部用例随之执行：读写往返与元数据、覆盖与不覆盖、批量删除的逐项结果、按前缀分页列举、不存在的错误、键的区分大小写与嵌套段、大对象流式读写。

```csharp
public sealed class FileSystemBlobStoreConformanceTests : BlobStoreConformance
{
    protected override IBlobStore CreateStore() => new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = TempDirectory() }, TimeProvider.System);
}
```

## 依赖方向

依赖 `XiHan.Framework.ObjectStorage.Abstractions` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
