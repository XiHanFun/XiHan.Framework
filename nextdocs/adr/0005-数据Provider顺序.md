# ADR-0005 持久化 Provider 以 EntityFrameworkCore 优先

## 状态

已采纳

## 日期

2026-10-21

## 背景

旧框架绑定 SqlSugar，存量用户依赖它；.NET 生态的主流 ORM 是 EF Core。持久化抽象必须先用一个 Provider 验证。

## 备选

1. SqlSugar 优先，照顾存量。
2. EntityFrameworkCore 优先，SqlSugar 作为存量迁移 Provider 在 P5 提供。

## 决策

采用备选 2。`Persistence.EntityFrameworkCore` 在 P2 作为首个 Provider 验证 `Persistence.Abstractions` 与一致性套件；`Persistence.SqlSugar` 在 P5 随迁移工具提供。

## 理由

- EF Core 的迁移、变更跟踪、AOT 工作都在官方路线图上，适合做抽象的基准实现。
- 抽象先被最主流的实现验证，再被第二个实现验证，更能暴露抽象泄漏。

## 影响

- 一致性套件 `Persistence.Conformance` 以 EF Core 与 InMemory 两个实现同时通过为准。
- SqlSugar 用户的迁移路径写进迁移指南。
