# XiHan.Framework.ObjectStorage.FileSystem

## 关于

`XiHan.Framework.ObjectStorage.FileSystem` 是对象存储的默认实现：本地目录按容器与键存放内容与元数据，带总量与单对象配额。适用于开发、测试、单实例部署，以及挂了共享卷的小规模部署；多实例、对象量大或需要客户端直连时用 S3。

## 主要特性

* 原子替换写入，读取方要么看到旧对象要么看到完整的新对象
* 不覆盖写入原子，多进程共享目录也只有一个成功
* 总量与单对象配额，达到上限健康项 Degraded
* 支持分片上传与命名实例

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.ObjectStorage.FileSystem --prerelease
```

### 使用

启用后即可注入 `IBlobStore`：

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<FileSystemObjectStorageFeature>());
```

```json
{ "XiHan": { "ObjectStorage": { "FileSystem": { "RootPath": "/var/lib/app/blobs", "MaxTotalBytes": 1073741824, "MaxBlobBytes": 268435456 } } } }
```

预签名地址需要装配 `Hosting.AspNetCore` 的 `HostedBlobUrlFeature` 并 `MapXiHanBlobs()`，由宿主代收发。

## 主要类型

此库提供的主要类型：

* `FileSystemObjectStorageFeature`：特性入口
* `FileSystemObjectStorageOptions`：根目录与配额
* `FileSystemBlobStore`：`IBlobStore` 与 `IBlobMultipartStore` 的实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/objectstorage-filesystem)

## 相关包

* 对象存储契约：[XiHan.Framework.ObjectStorage.Abstractions](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.Abstractions)
* S3 实现：[XiHan.Framework.ObjectStorage.S3](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.S3)
* 宿主：[XiHan.Framework.Hosting.AspNetCore](https://www.nuget.org/packages/XiHan.Framework.Hosting.AspNetCore)

## 反馈与贡献

`XiHan.Framework.ObjectStorage.FileSystem` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
