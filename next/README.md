# XiHan Framework 5（next 工作区）

下一代框架的 .NET 参考实现工作区。设计文档在仓库根的 [`nextdocs/`](../nextdocs/下一代框架总体方案.md)，规范草案在本目录 `spec/`。

## 布局

```text
next/
├─ XiHan.Framework.Next.slnx        # 解决方案目录按环层分组
├─ Directory.Build.props            # 版本线 5.x、Nullable、警告即错误、文档文件
├─ Directory.Packages.props         # 中央包版本，Ring 0/1 只允许 Microsoft.Extensions.* 抽象包
├─ spec/                            # XiHan Spec：RFC、术语表、清单 Schema、命名约定
├─ src/                             # 正式包，物理目录扁平，包名由术语表 + 文法推出
├─ test/                            # 单元、架构、集成、基准、AOT 冒烟
├─ samples/                         # 示例（P1 起）
└─ incubator/                       # 实验包，只能标 Experimental
```

## 命令

在仓库根执行：

```bash
dotnet restore next/XiHan.Framework.Next.slnx
dotnet build next/XiHan.Framework.Next.slnx -c Release --no-restore
dotnet test --solution next/XiHan.Framework.Next.slnx --configuration Release --no-build
dotnet pack next/XiHan.Framework.Next.slnx -c Release --no-build
```

`global.json` 已启用 Microsoft.Testing.Platform，`dotnet test` 必须用 `--solution`。

## 门禁

| 门禁 | 位置 |
| --- | --- |
| 警告即错误、Nullable、NU1903/NU1904 | `Directory.Build.props` |
| PublicAPI 文件、包验证、AOT 分析器 | `src/Directory.Build.props` |
| 依赖方向、包 ID 文法、命名空间、零反射、第三方类型泄漏、版权头 | `test/XiHan.Framework.Architecture.Tests` |
| CI | `.github/workflows/next-ci.yml` |

## 阶段

当前处于 P1 内核：原语层已完成，内核进行中。各阶段退出条件见 [`nextdocs/architecture/05-开发计划.md`](../nextdocs/architecture/05-开发计划.md)。
