# XiHan.Framework.ObjectStorage.Conformance

## 关于

`XiHan.Framework.ObjectStorage.Conformance` 是对象存储的一致性测试套件，给写对象存储提供程序的人验证实现用，不是业务代码的依赖。

## 主要特性

* `BlobStoreConformance`：读写往返、覆盖与不覆盖、并发写入、分页列举、范围读取、分片上传
* `BlobStoreProviderConformance`：命名实例的建出、复用、替换与移除

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.ObjectStorage.Conformance --prerelease
```

### 使用

继承 `BlobStoreConformance`，实现 `CreateStore()` 与 `PlantOutsideContractAsync`（绕过契约直接在底层存储写一个对象）：

```csharp
public sealed class FileSystemBlobStoreConformanceTests : BlobStoreConformance
{
    protected override IBlobStore CreateStore()
        => new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = TempDirectory() }, TimeProvider.System);

    protected override Task PlantOutsideContractAsync(ContainerName container, string key, byte[] content)
        => WriteUnderDataDirectoryAsync(container, key, content);
}
```

支持命名实例的提供程序再继承 `BlobStoreProviderConformance`。

## 主要类型

此库提供的主要类型：

* `BlobStoreConformance`
* `BlobStoreProviderConformance`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 对象存储契约：[XiHan.Framework.ObjectStorage.Abstractions](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.Abstractions)
* 文件系统实现：[XiHan.Framework.ObjectStorage.FileSystem](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.FileSystem)
* S3 实现：[XiHan.Framework.ObjectStorage.S3](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.S3)

## 反馈与贡献

`XiHan.Framework.ObjectStorage.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
