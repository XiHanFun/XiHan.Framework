# XiHan.Framework.Data.EntityFrameworkCore

## 项目定位

`XiHan.Framework.Data.EntityFrameworkCore` 提供基于微软官方 `Entity Framework Core` 的数据访问实现。

## 责任边界

本项目负责：

1. 将数据访问抽象落地到 `EF Core`
2. 提供 `DbContext`、仓储、事务、模型配置等统一扩展基础

本项目不负责：

1. 具体业务模块实体映射内容
2. 数据访问抽象定义

## 依赖策略说明

本项目优先采用微软官方包，是框架默认优先推荐的数据实现方向。
后续若同时保留 `SqlSugar`，则两者共享相同的抽象层，而不是各自独立演化。

## 当前阶段

当前项目已建立最小 `EF Core` 接入骨架，后续将继续补充 `DbContext` 约定、仓储实现和事务协作能力。
