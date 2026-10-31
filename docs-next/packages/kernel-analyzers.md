# XiHan.Framework.Kernel.Analyzers

Roslyn 分析器：把公开 API 的稳定性标注、类型命名与 Ring 0/1 零反射这些规则交给编译器执行，诊断 XH0001–XH0005 全部是错误级。框架的每个正式包都以分析器形式引用它；写框架扩展包的人可以在自己的项目里启用同一套规则。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Kernel.Analyzers` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（分析器 `PublicTypeAnalyzer`、`InternalApiUsageAnalyzer`、`ReflectionUsageAnalyzer`） |
| 配置节 | —（范围由 MSBuild 属性 `XiHanFrameworkPackage`、`XiHanRing` 控制） |
| 依赖 | 只在构建期使用 `Microsoft.CodeAnalysis.CSharp` 4.14.0；目标 `netstandard2.0`，开发依赖，不进入运行时 |
| AOT | 不适用（编译期组件） |
| 稳定性 | —（编译期组件，不标注 `[ApiLevel]`；五条规则可用） |

## 概述

| 规则 | 范围 | 含义 | 消息 |
| --- | --- | --- | --- |
| `XH0001` | 框架包 | 公开类型必须标注 `[ApiLevel]` | `Public type '{0}' must be annotated with [ApiLevel]` |
| `XH0002` | 框架包 | 公开类型名不得以 `XiHan` 开头：归属由命名空间表达 | `Type name '{0}' must not start with 'XiHan' because the namespace already identifies ownership` |
| `XH0003` | 所有引用了分析器的项目 | 不得在定义程序集之外使用标注为 `Stability.Internal` 的类型或成员 | `'{0}' is annotated with Internal stability and must not be used outside its own assembly` |
| `XH0004` | 框架包 | 公开类型名不得用禁用词：`Default` 前缀，`Manager`、`Helper`、`Utils`、`Util`、`Common`、`Shared`、`Impl`、`Base` 后缀 | `Type name '{0}' uses the forbidden word '{1}'` |
| `XH0005` | Ring 0/1 | 不得使用反射 API | `'{0}' is a reflection API, which is forbidden in the primitives and kernel rings` |

全部规则的类别是 `XiHan`、级别 Error，帮助链接指向本包 README 的规则表。判定细节：

- XH0001 / XH0002 / XH0004 只看「有效公开」的类型（自身与全部包含类型都是 `public`），跳过编译器生成的类型；编译里找不到 `ApiLevelAttribute`（没引用原语层）时不报 XH0001。名字恰好等于禁用词本身（如 `Base`）不算；一个类型只报一次 XH0004。
- XH0003 按 `[ApiLevel]` 构造参数的整数值判定 `Internal`（`Stability.Internal = 3`），以成员的原始定义判定，泛型实例化后仍能命中；类型或其任一包含类型标为 Internal 时，类型名出现在源码里也报。程序集内部可以用自己的 Internal API。
- XH0005 把 `System.Reflection` 命名空间下的一切视为反射，只放行 `MemberInfo` 上的 `Name`、`DeclaringType`、`ReflectedType`、`MemberType`、`MetadataToken`、`IsCollectible`；`Activator` 的任何成员都是反射；`Type` 上的成员查找（`GetMethod`、`GetProperty`、`GetCustomAttributes`、`MakeGenericType`、`Assembly` 等，以及带参数的 `Type.GetType(string)`）是反射，`object.GetType()` 不是。

## 何时使用

- 向框架仓库提交正式包：已由 `next/src/Directory.Build.props` 统一引用并设置 `XiHanFrameworkPackage=true`，不需要再做什么。
- 自己维护框架扩展包，想遵守同样的命名与稳定性规则：按下文启用。
- 应用想在编译期挡住对 Internal API 的使用：只引用分析器即可得到 XH0003，命名规则不会作用于应用代码。

## 安装与启用

```xml
<ItemGroup>
  <PackageReference Include="XiHan.Framework.Kernel.Analyzers" Version="5.0.0-preview.1" PrivateAssets="all" />
</ItemGroup>
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。框架包对分析器的引用带 `PrivateAssets="all"`，不会传递给应用；应用要用 XH0003 时自己引用。

框架包规则与零反射规则只在对应的 MSBuild 属性存在时生效。分析器经 `build_property.*` 读取它们，所以属性还要声明为编译器可见（框架仓库在 `next/src/Directory.Build.props` 里这样做；分析器包本身不带声明这两个属性的 `.props`）：

```xml
<PropertyGroup>
  <!-- 启用 XH0001 / XH0002 / XH0004 -->
  <XiHanFrameworkPackage>true</XiHanFrameworkPackage>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="XiHanFrameworkPackage" />
  <CompilerVisibleProperty Include="XiHanRing" />
</ItemGroup>
```

`XiHanRing` 只有原语层（`0`）与内核（`1`）设置，用来启用 XH0005；更高环层允许反射，例如 `Plugins.InProcess` 在标注 `RequiresDynamicCode` 后使用。

## 配置

| MSBuild 属性 | 值 | 作用 |
| --- | --- | --- |
| `XiHanFrameworkPackage` | `true`（不区分大小写） | 启用 XH0001、XH0002、XH0004 |
| `XiHanRing` | `0` 或 `1` | 启用 XH0005；未设置或不是整数视为不在 Ring 0/1 |

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `PublicTypeAnalyzer` | XH0001、XH0002、XH0004 |
| `InternalApiUsageAnalyzer` | XH0003 |
| `ReflectionUsageAnalyzer` | XH0005 |

## 使用示例

在设置了 `XiHanFrameworkPackage=true` 的扩展包里：

```csharp
using XiHan.Framework.Primitives;

namespace Contoso.Reports;

// 通过：标注了稳定性，名字不带前缀与禁用词
[ApiLevel(Stability.Preview, "1.0")]
public sealed class ReportWriter;

// XH0001：缺少 [ApiLevel]
public sealed class ReportExporter;

// XH0004：禁用后缀 Manager
[ApiLevel(Stability.Preview, "1.0")]
public sealed class ReportManager;

// XH0004：Default 前缀；默认实现的角色写在清单的 role: default，不进类型名
[ApiLevel(Stability.Preview, "1.0")]
public sealed class DefaultReportStore;
```

## 行为与限制

- 全部规则是 Error 级。框架仓库把警告也当错误（`TreatWarningsAsErrors`），也不允许以 `NoWarn` 关掉这些诊断。
- 分析器只检查源码里的声明与调用，不检查经反射或动态代码间接触达的 API。
- 规则的单元测试在 `next/test/XiHan.Framework.Kernel.Analyzers.Tests`，覆盖每条规则的报出与不报出两侧。

## 相关

- [测试与一致性套件](../guide/testing#架构测试与门禁)、[扩展与二次开发](../guide/extending)
- [Kernel.Generators](./kernel-generators)、[Primitives](./primitives)
