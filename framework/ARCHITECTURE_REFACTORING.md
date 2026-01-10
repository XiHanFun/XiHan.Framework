# XiHan.Framework 架构重构说明

## 重构日期

2026-01-11

## 重构目标

按照 .NET 官方设计规范和 DDD 最佳实践，优化框架架构，解耦模块依赖，提高可维护性和可测试性。

## 重构内容

### 1. 解耦 Caching 和 Uow

**变更**:

- 将 `UnitOfWorkCacheItem<T>` 从 `XiHan.Framework.Caching` 移至 `XiHan.Framework.Uow`
- 将 `UnitOfWorkCacheItemExtensions` 从 `XiHan.Framework.Caching.Extensions` 移至 `XiHan.Framework.Uow.Extensions`
- 简化 Caching 模块的项目依赖

**理由**:

- Caching 作为基础设施组件,不应该定义 UOW 相关的类型
- UOW 相关的缓存项应该由 UOW 模块自身管理
- 降低模块间的耦合度

### 2. 移除 Application 层对 Data 层的直接依赖

**变更**:

- 从 `XiHan.Framework.Application.csproj` 中移除对 `XiHan.Framework.Data` 的项目引用
- 从 `XiHan.Framework.Application.csproj` 中移除对 `XiHan.Framework.MultiTenancy` 的项目引用

**理由**:

- 符合 DDD 分层架构原则: Application 层应该通过 Domain 层的仓储接口访问数据
- Application 层不应该直接依赖基础设施层(Data)
- 降低应用层与数据访问层的耦合

### 3. 简化 EventBus 依赖

**变更**:

- 从 `XiHan.Framework.EventBus.csproj` 移除以下依赖:
  - `XiHan.Framework.Data`
  - `XiHan.Framework.DistributedIds`
  - `XiHan.Framework.MultiTenancy`
  - `XiHan.Framework.ObjectMapping`
  - `XiHan.Framework.Tasks`
- 仅保留核心依赖:
  - `XiHan.Framework.Core`
  - `XiHan.Framework.Messaging`
  - `XiHan.Framework.Uow`

**理由**:

- EventBus 作为基础设施组件,应该保持轻量级
- 不应该依赖 Data、ObjectMapping 等应用层关注的模块
- 特定功能可通过事件处理器在应用层实现

### 4. 重构 Settings 依赖

**变更**:

- 从 `XiHan.Framework.Settings.csproj` 移除对 `XiHan.Framework.Data` 和 `XiHan.Framework.Uow` 的依赖
- 从 `XiHanSettingsModule` 移除对 `XiHanDataModule` 的依赖
- 保留对 `XiHan.Framework.Security` 的依赖(用于 `ICurrentUser`)

**理由**:

- Settings 作为配置管理模块,不应该直接依赖数据访问层
- 设置的持久化应该通过 `ISettingStore` 接口抽象
- 依赖 Security 是合理的,因为用户设置需要知道当前用户

### 5. 简化 ObjectMapping 依赖

**变更**:

- 从 `XiHan.Framework.ObjectMapping.csproj` 移除以下依赖:
  - `XiHan.Framework.Localization`
  - `XiHan.Framework.Validation`

**理由**:

- ObjectMapping 应该是纯粹的对象映射工具
- 本地化和验证应该在应用层处理
- 降低基础设施组件之间的耦合

### 6. 重构 MultiTenancy

**变更**:

- 从 `XiHan.Framework.MultiTenancy.csproj` 移除对 `XiHan.Framework.Data` 的依赖
- 从 `XiHanMultiTenancyModule` 移除对 `XiHanDataModule` 的依赖
- 将 `ConnectionStrings` 类移至 `XiHan.Framework.Core.Data` 命名空间
- 在 `XiHan.Framework.Data` 中保留向后兼容的别名

**理由**:

- MultiTenancy 作为横切关注点,不应该直接依赖数据访问层
- `ConnectionStrings` 是核心配置类,应该在 Core 模块中
- 多租户的数据隔离应该通过接口抽象实现

### 7. 解耦 VirtualFileSystem 和 Security

**变更**:

- 从 `XiHan.Framework.VirtualFileSystem.csproj` 移除对 `XiHan.Framework.Security` 的依赖

**理由**:

- VirtualFileSystem 应该是独立的基础设施组件
- 文件访问控制应该在更上层(Application 或 Web 层)处理
- 降低基础设施组件之间的耦合

### 8. 创建 Application.Contracts 模块

**变更**:

- 创建新模块 `XiHan.Framework.Application.Contracts`
- 将 `XiHan.Framework.Domain` 中的 `Paging` 文件夹移至新模块
- 包含以下内容:
  - `Paging/Dtos`: PageQuery, PageResponse, PageData, PageInfo, SelectCondition, SortCondition
  - `Paging/Enums`: SortDirection, SelectCompare
  - `Paging/Handlers`: CollectionPropertySelector, CollectionPropertySortor, SelectConditionParser, SortConditionParser
- 在 Domain 层创建向后兼容的别名类
- 更新命名空间: `XiHan.Framework.Domain.Paging` → `XiHan.Framework.Application.Contracts.Paging`

**理由**:

- 符合 DDD 原则: Domain 层应该只包含领域模型和业务逻辑
- 分页、查询对象属于应用层关注点
- Application.Contracts 模块遵循 ABP Framework 的设计模式
- IQueryable 扩展方法更适合放在应用层

## 新的架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                      表示层 (Presentation)                   │
│  Web.Api, Web.Grpc, Web.Docs, Web.RealTime, Web.Gateway    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                       应用层 (Application)                   │
│          Application, Application.Contracts                  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                       领域层 (Domain)                        │
│               Domain (聚合、实体、领域服务)                   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                  基础设施层 (Infrastructure)                 │
│  Data, EventBus, Messaging, Caching, Tasks, SearchEngines   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      核心层 (Core)                           │
│  Core, Utils, Metadata, Uow, Security, Settings, Threading  │
│  Localization, Serialization, DistributedIds, Validation    │
│  MultiTenancy, ObjectMapping, VirtualFileSystem             │
└─────────────────────────────────────────────────────────────┘
```

## 关键依赖关系

### Application 层

- 依赖: Core, Application.Contracts, Domain, DistributedIds, Logging, ObjectMapping
- 不再依赖: Data, MultiTenancy

### Application.Contracts 层 (新增)

- 依赖: Core
- 包含: DTO、查询对象、分页相关类

### Domain 层

- 依赖: Core, Application.Contracts (仅用于向后兼容别名)
- 职责: 聚合根、实体、值对象、领域服务、仓储接口

### EventBus 层

- 依赖: Core, Messaging, Uow
- 不再依赖: Data, DistributedIds, MultiTenancy, ObjectMapping, Tasks

### Settings 层

- 依赖: Core, Security
- 不再依赖: Data, Uow

### MultiTenancy 层

- 依赖: Core, Security, Settings
- 不再依赖: Data

### ObjectMapping 层

- 依赖: Core
- 不再依赖: Localization, Validation

### VirtualFileSystem 层

- 依赖: Core
- 不再依赖: Security

## 向后兼容性

为了保持向后兼容,以下类型创建了别名:

1. **Domain.Paging** → **Application.Contracts.Paging**

   - 在 `XiHan.Framework.Domain\Paging\BackwardCompatibilityAliases.cs` 中创建了别名类
   - 标记为 `[Obsolete]` 提示开发者使用新命名空间

2. **Data.ConnectionStrings** → **Core.Data.ConnectionStrings**
   - 在 `XiHan.Framework.Data\ConnectionStrings.cs` 中创建了继承别名
   - 标记为 `[Obsolete]` 提示开发者使用新位置

## 迁移指南

### 对于使用分页功能的代码

```csharp
// 旧代码
using XiHan.Framework.Domain.Paging;

// 新代码
using XiHan.Framework.Application.Contracts.Paging;
using XiHan.Framework.Application.Contracts.Paging.Dtos;
```

### 对于使用 ConnectionStrings 的代码

```csharp
// 旧代码
using XiHan.Framework.Data;

// 新代码
using XiHan.Framework.Core.Data;
```

### 对于使用 UnitOfWorkCacheItem 的代码

```csharp
// 旧代码
using XiHan.Framework.Caching;

// 新代码
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Extensions;
```

## 优势

1. **清晰的分层**: 严格遵循 DDD 分层架构
2. **降低耦合**: 移除不必要的模块依赖
3. **提高可测试性**: 每个模块职责更加单一
4. **符合国际标准**: 遵循 .NET 官方和 ABP Framework 的设计模式
5. **向后兼容**: 通过别名和废弃标记保持兼容性
6. **易于维护**: 模块职责清晰,易于理解和维护

## 注意事项

1. 旧代码中的 `[Obsolete]` 类型应该逐步迁移到新命名空间
2. 数据访问应该通过 Domain 层的仓储接口,而不是直接依赖 Data 层
3. 横切关注点(如多租户、缓存)应该通过接口抽象,在具体实现中集成
4. 基础设施组件应该保持轻量级,避免相互依赖

## 后续改进建议

1. 考虑将 Caching 中对 MultiTenancy 和 Uow 的使用改为可选接口注入
2. 考虑创建 Domain.Contracts 模块来存放仓储接口
3. 考虑将 Specification 从 Domain 独立出来
4. 评估是否需要创建 Infrastructure.Abstracts 模块来存放基础设施抽象
