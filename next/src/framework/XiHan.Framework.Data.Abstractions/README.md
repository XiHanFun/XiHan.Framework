# XiHan.Framework.Data.Abstractions

## 项目定位

`XiHan.Framework.Data.Abstractions` 是数据访问层统一抽象，供不同 ORM 或数据访问技术实现复用。

## 责任边界

本项目负责：

1. 数据访问相关抽象接口
2. 查询与仓储统一约定
3. 数据上下文最小抽象

本项目不负责：

1. `SqlSugar` 具体实现
2. `Entity Framework Core` 具体实现
3. 数据库连接与迁移工具实现

## 依赖方向

本项目位于所有数据实现之前，具体 Provider 必须依赖它，而不是反过来。

## 当前阶段

当前项目已建立最小数据上下文访问抽象，后续将继续补充仓储、查询和事务协作相关抽象。
