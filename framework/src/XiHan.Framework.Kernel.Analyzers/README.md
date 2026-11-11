# XiHan.Framework.Kernel.Analyzers

## 关于

`XiHan.Framework.Kernel.Analyzers` 是 Roslyn 分析器：把公开 API 的稳定性标注、类型命名与零反射规则交给编译器执行。写框架扩展包的人可以在自己的项目里启用同一套规则；应用引用它即可在编译期挡住对内部 API 的使用。

## 主要特性

* `XH0001`：公开类型必须标注 `[ApiLevel]`
* `XH0002` / `XH0004`：公开类型名不得以 `XiHan` 开头，不得使用 `Manager`、`Helper`、`Default` 等禁用词
* `XH0003`：不得在程序集之外使用标注为 `Stability.Internal` 的 API
* `XH0005`：原语层与内核不得使用反射 API
* `XH0006` / `XH0007`：`Stable` 的签名不得引用 `Preview` / `Experimental` 类型，成员的级别不得高于所在类型
* `XH0008`：`Stable` 的公开方法、构造与索引器不得声明可选参数

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Kernel.Analyzers --prerelease
```

### 使用

引用后 `XH0003` 对所有引用者生效；命名与零反射规则由 MSBuild 属性开启（包里已把它们声明为编译器可见）：

```xml
<PropertyGroup>
  <XiHanFrameworkPackage>true</XiHanFrameworkPackage>   <!-- 启用 XH0001 / XH0002 / XH0004 / XH0006～XH0008 -->
  <XiHanRing>1</XiHanRing>                              <!-- 启用 XH0005，仅原语层与内核 -->
</PropertyGroup>
```

触发示例（缺少稳定性标注，报 `XH0001`）：

```csharp
public sealed class ReportExporter;   // XH0001
```

分析器是开发依赖，不进入运行时。

## 主要类型

此库提供的主要类型：

* `PublicTypeAnalyzer`：`XH0001`、`XH0002`、`XH0004`
* `InternalApiUsageAnalyzer`：`XH0003`
* `ReflectionUsageAnalyzer`：`XH0005`
* `StabilityDependencyAnalyzer`：`XH0006`、`XH0007`
* `OptionalParameterAnalyzer`：`XH0008`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/kernel-analyzers)

## 相关包

* 操作登记的源生成器：[XiHan.Framework.Kernel.Generators](https://www.nuget.org/packages/XiHan.Framework.Kernel.Generators)
* 内核：[XiHan.Framework.Kernel](https://www.nuget.org/packages/XiHan.Framework.Kernel)

## 反馈与贡献

`XiHan.Framework.Kernel.Analyzers` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
