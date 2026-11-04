# XiHan.Framework.Domain

## 关于

`XiHan.Framework.Domain` 定义领域模型的契约：实体与聚合根、领域事件、值对象、持久化自动维护字段的协议接口，以及可组合的查询规约。只有接口与零依赖的组合器，没有实体基类，按需组合协议即可。

## 主要特性

* `IEntity<TKey>`、`IAggregateRoot<TKey>` 标识与聚合根
* `DomainEventBuffer` 收集并清空领域事件
* 软删除、创建/修改时间、多租户、并发戳等协议，持久化提供程序按协议维护
* `Specification<T>` 支持 And / Or / Not 组合

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Domain --prerelease
```

### 使用

通常由持久化等抽象包带入；业务代码里让实体实现需要的协议：

```csharp
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, ISoftDeletable, IHasCreationTime
{
    private readonly DomainEventBuffer _events = new();
    public Guid Id { get; init; }
    public string TenantId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;
    public void ClearDomainEvents() => _events.Clear();
}
```

## 主要类型

此库提供的主要类型：

* `IEntity<TKey>`、`IAggregateRoot<TKey>`
* `IDomainEvent`、`IHasDomainEvents`、`DomainEventBuffer`
* `ISoftDeletable`、`IHasCreationTime`、`IHasModificationTime`、`IMultiTenant`、`IHasConcurrencyStamp`
* `IValueObject`：值对象标记，用 `record` 获得值相等
* `ISpecification<T>`、`Specification<T>`、`Specification`：查询规约

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/domain)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)

## 反馈与贡献

`XiHan.Framework.Domain` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
