# XiHan.Framework.ObjectStorage.Abstractions

## 关于

`XiHan.Framework.ObjectStorage.Abstractions` 定义对象存储契约：容器与键、批量读写删除与分页列举、范围读取、分片上传、预签名地址与命名存储实例。不含存储实现，由文件系统或 S3 提供程序提供。

## 主要特性

* 写入与删除是批量的，逐项返回结果
* 不覆盖写入必须原子：并发写同一个新键只有一个成功
* 范围读取、分片上传（可续传）、限时预签名地址
* 按名字管理多个存储实例

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.ObjectStorage.Abstractions --prerelease
```

### 使用

一般由实现包带入；业务代码注入 `IBlobStore` 读写对象：

```csharp
var put = await store.PutAsync(new ContainerName("invoices"), new BlobKey("2026/10/inv-1.pdf"), stream, new BlobPutOptions { ContentType = "application/pdf" }, ct);
var page = await store.ListAsync(new ContainerName("invoices"), new BlobQuery { Prefix = "2026/10/", PageSize = 100 }, ct);
var deleted = await store.DeleteAsync(new ContainerName("invoices"), [key1, key2], ct);   // 逐项结果
```

## 主要类型

此库提供的主要类型：

* `IBlobStore`：读写、元数据、删除、列举
* `IBlobMultipartStore`：分片上传
* `IBlobUrlSigner`：预签名地址
* `BlobStores`、`BlobStoreDefinition`：命名存储实例
* `ContainerName`、`BlobKey`、`BlobPutOptions`、`BlobQuery`：标识与选项

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/objectstorage-abstractions)

## 相关包

* 文件系统实现：[XiHan.Framework.ObjectStorage.FileSystem](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.FileSystem)
* S3 实现：[XiHan.Framework.ObjectStorage.S3](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.S3)
* 一致性套件：[XiHan.Framework.ObjectStorage.Conformance](https://www.nuget.org/packages/XiHan.Framework.ObjectStorage.Conformance)

## 反馈与贡献

`XiHan.Framework.ObjectStorage.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
