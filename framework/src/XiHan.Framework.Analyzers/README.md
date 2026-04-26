# XiHan.Framework.Analyzers

曦寒框架 Roslyn 分析器，用于检查 C# 文件是否包含标准版权版本注释，并在 IDE 中提供 Code Fix 自动修复。

## 规则

- `XHFH001`：文件缺少或未正确声明曦寒标准版权文件头。

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

## 项目引用

开发期可通过项目引用接入：

```xml
<ProjectReference Include="..\..\tool\XiHan.Framework.Analyzers\XiHan.Framework.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"
                  PrivateAssets="all" />
```

发布后也可以通过 NuGet 包接入。
