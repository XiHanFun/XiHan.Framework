# XiHan.Framework.Analyzers

曦寒框架 Roslyn 分析器，用于检查 C# 文件是否以标准版权文件头开头，并在 IDE 中提供 Code Fix 自动修复。

## 规则

- `XHFH001`：文件缺少或未正确声明曦寒标准版权文件头。
- `XHFA001`：直接 new HttpClient（默认 Info）。
- `XHFA002`：对外可见的异步方法已有取消令牌参数，调用带可选取消令牌参数的方法时省略了该参数（默认 Info，本仓库 `.editorconfig` 设为 warning）；只检查方法调用，不检查构造函数与隐式调用（如集合初始化器的 Add）；提供 Code Fix，以命名实参转发令牌。

## 级别控制

在 `.editorconfig` 中控制诊断级别：

```ini
[*.cs]
dotnet_diagnostic.XHFH001.severity = warning
```

需要编译失败时改为：

```ini
dotnet_diagnostic.XHFH001.severity = error
```

需要排除目录时按目录覆盖：

```ini
[**/Migrations/*.cs]
dotnet_diagnostic.XHFH001.severity = none
```

## 编译器兼容性

分析器编译自 Microsoft.CodeAnalysis 5.0，可被 .NET SDK 10.0.100 及以上自带的编译器加载。本仓库的全部项目经 `props/common.props` 以分析器方式引用它（不进入任何包的依赖）；编译器低于分析器所需版本时构建报 `CS9057` 错误，而不是静默停用分析器。

## 项目引用

开发期可通过项目引用接入：

```xml
<ProjectReference Include="..\..\tool\XiHan.Framework.Analyzers\XiHan.Framework.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"
                  PrivateAssets="all" />
```

发布后也可以通过 NuGet 包接入。
