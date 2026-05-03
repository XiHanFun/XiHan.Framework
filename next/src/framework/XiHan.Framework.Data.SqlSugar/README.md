# XiHan.Framework.Data.SqlSugar

## 项目定位

`XiHan.Framework.Data.SqlSugar` 提供基于 `SqlSugar` 的数据访问实现。

## 责任边界

本项目负责：

1. 将数据访问抽象落地到 `SqlSugar`
2. 提供仓储、查询、事务等 `SqlSugar` 适配实现

本项目不负责：

1. 定义数据访问抽象本身
2. 定义领域仓储契约本身

## 依赖策略说明

`SqlSugar` 不是微软官方包，但在当前国内 .NET 社区中活跃度较高、实践广泛，因此作为可选 Provider 保留。
同时，为避免单一技术绑定，框架会同步保留 `EF Core` 实现。

## 当前阶段

当前项目已建立最小 `SqlSugar` 接入骨架，后续将继续补充仓储、事务和多租户协作实现。
