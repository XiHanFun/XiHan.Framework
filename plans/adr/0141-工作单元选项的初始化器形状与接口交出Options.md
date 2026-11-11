# ADR-0141 工作单元选项的初始化器形状与接口交出 Options

## 状态

已采纳（规范 `rfcs/0005-persistence.md` 修订）。

## 日期

2026-11-11

## 背景

`UnitOfWorkOptions`（`Stable`）是位置记录 `(TenantId, IsReadOnly, Connection)`，后来又追加了 `init` 属性 `ReadsPrimary`（ADR-0122），成了一半位置参数、一半 `init` 的样子。ADR-0084 加 `Connection` 时就改过一次构造，GA 之后再加位置参数就是二进制破坏。

`IUnitOfWork`（`Stable`，Provider 实现）还把选项的字段逐个镜像成自己的成员（`TenantId`、`IsReadOnly`）。选项每加一个字段，接口就要跟着加一个成员，这对 Provider 是破坏。

## 决策

- `UnitOfWorkOptions` 改为只有 `init` 属性的记录：`TenantId`、`IsReadOnly`、`Connection`（非空白校验留在 `init` 里）、`ReadsPrimary`。不再有位置构造与 `Deconstruct`，`Default` 不变。
- `IUnitOfWork` 删掉 `TenantId`、`IsReadOnly`，改为 `UnitOfWorkOptions Options { get; }`：交出开启时的选项，未开启时是 `Default`。以后选项加字段，接口不变。
- 三个 Provider 的工作单元实现 `Options`。仓储用的 `TenantId` / `IsReadOnly` 留作这些类型的 internal 便捷成员。
- 框架内 52 处构造机械改写成初始化器，原来紧跟的 `{ ReadsPrimary = true }` 一并并入；经接口读 `TenantId` / `IsReadOnly` 的地方改读 `Options`。

## 影响

- 源码迁移：
  - `new UnitOfWorkOptions("acme", IsReadOnly: true)` → `new UnitOfWorkOptions { TenantId = "acme", IsReadOnly = true }`。
  - `new(IsReadOnly: true) { ReadsPrimary = true }` → `new() { IsReadOnly = true, ReadsPrimary = true }`。
  - `unitOfWork.TenantId` / `IsReadOnly` → `unitOfWork.Options.TenantId` / `Options.IsReadOnly`。
- 自己实现 `IUnitOfWork` 的 Provider 实现 `Options`，删掉两个镜像成员。
- `Persistence.Abstractions` 的契约是 `Stable`，GA 前修改，不需要登记豁免（ADR-0131）。
