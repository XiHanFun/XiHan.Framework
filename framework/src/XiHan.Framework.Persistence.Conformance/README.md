# XiHan.Framework.Persistence.Conformance

## 关于

`XiHan.Framework.Persistence.Conformance` 持久化的一致性套件，给写 Provider 的人用：任何提供关系型持久化的 Provider 都应继承 `RepositoryConformance` 并通过全部用例，这是「换 Provider 只改配置与包引用」的保证。

## 主要特性

* `RepositoryConformance`：仓储与工作单元语义，含事务、并发、软删除、租户、查询与只读操作
* `DataSourceConformance`、`SchemaConformance`：运行时数据源与库结构读取
* `IdGeneratorConformance<TKey>`：标识生成器，自定义的 `IIdGenerator<TKey>` 也应继承它

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.Conformance --prerelease
```

### 使用

测试项目引用被测实现与本包，写一个继承套件基类的类，全部用例随之执行。

```csharp
public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

## 主要类型

此库提供的主要类型：

* `RepositoryConformance`：仓储套件，需要实现 `CreateFeature()`
* `DataSourceConformance`：运行时数据源套件
* `SchemaConformance`：库结构读取与实体目录套件
* `IdGeneratorConformance<TKey>`：标识生成器套件

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)
* 内存实现：[XiHan.Framework.Persistence.InMemory](https://www.nuget.org/packages/XiHan.Framework.Persistence.InMemory)

## 反馈与贡献

`XiHan.Framework.Persistence.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
