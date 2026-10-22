# XiHan.Framework.Kernel.Analyzers

Roslyn 分析器（工具链），把命名与稳定性规则交给编译器执行。

| 规则 | 范围 | 含义 |
| --- | --- | --- |
| `XH0001` | 框架包 | 公开类型必须标注 `[ApiLevel]` |
| `XH0002` | 框架包 | 公开类型名不得以 `XiHan` 开头 |
| `XH0003` | 所有引用者 | 不得在程序集之外使用标注为 `Stability.Internal` 的 API |
| `XH0004` | 框架包 | 公开类型名不得使用禁用词：`Manager`、`Helper`、`Util`、`Utils`、`Common`、`Shared`、`Impl`、`Base` 后缀与 `Default` 前缀 |
| `XH0005` | Ring 0/1 | 不得使用反射 API（`System.Reflection`、`Activator`、`Type` 的成员查找） |

范围由 MSBuild 属性控制：`XiHanFrameworkPackage=true` 启用框架包规则，`XiHanRing=0|1` 启用零反射规则。`next/src/Directory.Build.props` 已为全部正式包设置。

## 依赖方向

只依赖 `Microsoft.CodeAnalysis.CSharp`。以分析器形式被所有正式包引用，不进入运行时依赖。

## 状态

P1：五条规则可用，附单元测试。
