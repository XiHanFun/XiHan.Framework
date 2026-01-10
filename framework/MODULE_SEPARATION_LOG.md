# 模块分离更新日志

## 2026-01-11 - 新增独立抽象模块

### 1. XiHan.Framework.Timing

**目的**: 将时间管理功能从 Core 模块独立出来

**分离内容**:

- `IClock` - 时钟抽象接口
- `Clock` - 时钟实现
- `ITimezoneProvider` - 时区提供者接口
- `TZConvertTimezoneProvider` - 基于 TimeZoneConverter 的实现
- `ICurrentTimezoneProvider` - 当前时区提供者接口
- `CurrentTimezoneProvider` - 当前时区提供者实现
- `XiHanClockOptions` - 时钟配置选项

**依赖关系**:

```
XiHan.Framework.Timing
    ↓
XiHan.Framework.Core
```

**NuGet 包**:

- TimeZoneConverter 7.2.0

**向后兼容**: Core 模块不再引用 Timing，原有代码需要更新引用

**迁移指南**:

```csharp
// 旧代码
using XiHan.Framework.Core.Timing;

// 新代码
using XiHan.Framework.Timing;
```

---

### 2. XiHan.Framework.MultiTenancy.Abstractions

**目的**: 将多租户抽象接口独立出来，供其他模块引用而不依赖具体实现

**分离内容**:

- `ICurrentTenant` - 当前租户接口
- `ICurrentTenantAccessor` - 当前租户访问器接口
- `BasicTenantInfo` - 基本租户信息
- `ITenantResolveContext` - 租户解析上下文接口
- `ITenantResolveContributor` - 租户解析贡献者接口
- `IMultiTenantUrlProvider` - 多租户 URL 提供者接口

**依赖关系**:

```
XiHan.Framework.MultiTenancy
    ↓
XiHan.Framework.MultiTenancy.Abstractions
    ↓
XiHan.Framework.Core
```

**向后兼容**: MultiTenancy 模块保留了向后兼容的别名，标记为 `[Obsolete]`

**迁移指南**:

```csharp
// 旧代码
using XiHan.Framework.MultiTenancy.Abstractions;

// 新代码（无需修改，命名空间相同）
using XiHan.Framework.MultiTenancy.Abstractions;
// 但现在引用的是独立包
```

---

### 3. XiHan.Framework.Localization.Abstractions

**目的**: 将本地化抽象接口独立出来，避免循环依赖

**分离内容**:

- `ILocalizableString` - 可本地化字符串接口
- `IHasNameWithLocalizableDisplayName` - 具有可本地化显示名称的接口

**依赖关系**:

```
XiHan.Framework.Localization
    ↓
XiHan.Framework.Localization.Abstractions
    ↓
XiHan.Framework.Core
```

**NuGet 包**:

- Microsoft.Extensions.Localization.Abstractions 10.0.1

**向后兼容**: Localization 模块保留了向后兼容的别名，标记为 `[Obsolete]`

**迁移指南**:

```csharp
// 旧代码
using XiHan.Framework.Localization;
public interface IMyInterface : ILocalizableString { }

// 新代码
using XiHan.Framework.Localization.Abstractions;
public interface IMyInterface : ILocalizableString { }
```

---

## 架构改进的意义

### 1. **职责分离**

- 抽象层不包含任何实现细节
- 具体实现模块依赖于抽象层
- 符合依赖倒置原则 (DIP)

### 2. **避免循环依赖**

- 其他模块可以引用抽象而不依赖具体实现
- 降低模块间的耦合度
- 提高可测试性

### 3. **更好的包管理**

- 抽象包体积小，依赖少
- 减少不必要的依赖传递
- 符合 NuGet 包设计最佳实践

### 4. **符合国际标准**

- 参考 Microsoft.Extensions.\* 的设计模式
- 遵循 .NET 官方的抽象层设计原则
- 与 ASP.NET Core 的架构保持一致

---

## 新的模块结构

```
框架核心层
├── XiHan.Framework.Core (核心功能)
├── XiHan.Framework.Utils (工具类)
├── XiHan.Framework.Metadata (元数据)
└── 抽象层
    ├── XiHan.Framework.Application.Contracts (应用契约)
    ├── XiHan.Framework.MultiTenancy.Abstractions (多租户抽象)
    └── XiHan.Framework.Localization.Abstractions (本地化抽象)

基础设施层
├── XiHan.Framework.Timing (时间管理)
├── XiHan.Framework.Threading (线程管理)
├── XiHan.Framework.Serialization (序列化)
└── ...

功能模块层
├── XiHan.Framework.MultiTenancy (多租户实现)
├── XiHan.Framework.Localization (本地化实现)
└── ...
```

---

## 后续建议

1. **继续抽象分离**:

   - 考虑创建 `XiHan.Framework.Security.Abstractions`
   - 考虑创建 `XiHan.Framework.Caching.Abstractions`
   - 考虑创建 `XiHan.Framework.EventBus.Abstractions`

2. **包版本策略**:

   - 抽象包应该更稳定，变更频率低
   - 实现包可以快速迭代
   - 保持向后兼容性

3. **文档更新**:
   - 更新每个模块的 README
   - 提供迁移指南
   - 说明最佳实践

---

## 总结

通过这次模块分离，框架的架构更加清晰：

1. ✅ **清晰的分层**: 抽象 → 实现
2. ✅ **降低耦合**: 按需引用抽象或实现
3. ✅ **提高可测试性**: 易于 Mock 抽象接口
4. ✅ **符合标准**: 遵循 .NET 官方设计模式
5. ✅ **向后兼容**: 通过别名保持兼容性

这些改进使框架更加符合企业级应用的要求，更易于维护和扩展！
