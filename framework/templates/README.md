# XiHan.Framework.Templates

XiHan.Framework 的 `dotnet new` 模板包，提供两个模板：

| 模板 | shortName | 生成内容 |
| --- | --- | --- |
| Web API 应用 | `xihan-web` | 启动模块、动态 API 示例、接口文档、集成测试（WebApplicationFactory + xUnit v3） |
| 模块 | `xihan-module` | 模块类、服务注册扩展、配置选项、默认服务、模块 README、测试项目 |

## 版本兼容

- 模板包版本取自 `framework/props/version.props`，与框架版本一致：`XiHan.Framework.Templates` X.Y.Z 生成的项目默认以精确版本（`[X.Y.Z]`）引用 X.Y.Z 的 `XiHan.Framework.*` 包。
- 需要引用其他已发布版本时传 `--framework-version <版本>`；该版本在包源中不存在时，还原阶段以 `NU1101`/`NU1102` 失败并列出查过的包源。
- 生成的项目带 `global.json`：.NET SDK 10.0.1xx（`latestPatch`，与本仓库一致），测试运行器为 Microsoft.Testing.Platform。本机没有 10.0.1xx 功能带时构建报 SDK not found：安装任一 10.0.1xx SDK；或把 `global.json` 的 `rollForward` 改为 `latestFeature` 以使用更高功能带的 SDK（此时会出现下述 `XHFH001` 警告）。
- 已知问题：`XiHan.Framework.Analyzers` 随框架包传递到生成的项目。在 SDK 10.0.1xx 下它因编译器版本不匹配而不加载，构建出现 `CS9057` 警告；在更高功能带的 SDK 下它会加载，生成的每个 `.cs` 文件都因不带曦寒版权文件头而报 `XHFH001`。

## 安装

模板包目前不发布到 nuget.org，从源码打包后按文件安装。打包得到的默认框架版本是当前 `version.props` 的版本；该版本尚未发布到 nuget.org 时，请检出已发布版本对应的 tag 再打包，或生成时传 `--framework-version <已发布的版本>`。

```bash
dotnet pack framework/templates/XiHan.Framework.Templates.csproj -c Release -o artifacts
dotnet new install artifacts/XiHan.Framework.Templates.<版本>.nupkg
dotnet new list xihan
```

## 使用

```bash
# 最小 Web API（不依赖任何外部服务）
dotnet new xihan-web -n MyCompany.Billing

# 加上数据访问（SQLite）、多租户、可观测性
dotnet new xihan-web -n MyCompany.Billing --data --multi-tenancy --observability

# 模块
dotnet new xihan-module -n MyCompany.Billing.Inventory
```

生成后：

```bash
cd MyCompany.Billing
dotnet build
dotnet test
dotnet run --project src/MyCompany.Billing
```

### xihan-web 选项

| 选项 | 默认 | 作用 |
| --- | --- | --- |
| `--data` | `false` | 引用 `XiHan.Framework.Data`，生成 SQLite 连接配置、`TodoItem` 实体与 `/api/TodoItem/Create`、`/api/TodoItem/List` 接口；启动时按 CodeFirst 建表，已存在的表只补齐缺失列，不删除表或数据 |
| `--multi-tenancy` | `false` | 在配置式租户存储中登记示例租户 `demo`（Id 1），生成 `/api/Tenant/Current`；租户由请求头 `X-Tenant-Id` 解析 |
| `--observability` | `false` | 引用 `XiHan.Framework.Observability`，映射 `/health` 健康检查端点；OpenTelemetry 默认关闭 |
| `--framework-version` | 模板包版本 | 引用的 `XiHan.Framework.*` 包版本 |

`--data` 与 `--multi-tenancy` 同时选择时，`TodoItem` 继承 `SugarMultiTenantEntity<long>`，数据按租户隔离。

### xihan-module 选项

| 选项 | 默认 | 作用 |
| --- | --- | --- |
| `--framework-version` | 模板包版本 | 引用的 `XiHan.Framework.*` 包版本 |

模块项目在包含 `Directory.Build.props`（定义 `XiHanFrameworkVersion`）的解决方案目录下生成时沿用该版本，否则使用自带的默认值。

把模块加进 `xihan-web` 生成的解决方案：

```bash
cd MyCompany.Billing
dotnet new xihan-module -n MyCompany.Billing.Inventory -o modules/MyCompany.Billing.Inventory
dotnet sln MyCompany.Billing.slnx add modules/MyCompany.Billing.Inventory/src/MyCompany.Billing.Inventory/MyCompany.Billing.Inventory.csproj
dotnet sln MyCompany.Billing.slnx add modules/MyCompany.Billing.Inventory/test/MyCompany.Billing.Inventory.Tests/MyCompany.Billing.Inventory.Tests.csproj
```

模块模板自带 `global.json`、`.gitignore` 与 `.slnx`，因此要生成到新的子目录；`-o .` 指向已有这些文件的目录时会被拒绝。

### 命名规则

`-n` 的值规整为合法的 C# 命名空间后用作项目名与命名空间（如 `1st-billing` → `_1st_billing`）；启动模块、模块类等类名取名称最后一段（`MyCompany.Billing` → `BillingModule`）。目标目录已有同名文件时 `dotnet new` 拒绝生成，不会覆盖。

名称最后一段不要与框架模块或 `Program` 重名，例如 `Contoso.XiHan`（生成 `XiHanModule`）、`Contoso.XiHanWebApi`（生成 `XiHanWebApiModule`）、`Program`，否则生成的类会与框架类型冲突。

## 更新

按文件安装的模板包不参与 `dotnet new update`，先卸载旧版再安装新版：

```bash
dotnet new uninstall XiHan.Framework.Templates
dotnet new install artifacts/XiHan.Framework.Templates.<新版本>.nupkg
```

已生成的项目改 `Directory.Build.props` 中的 `XiHanFrameworkVersion` 升级框架。

## 卸载

```bash
dotnet new uninstall XiHan.Framework.Templates
```

## 验证

```bash
pwsh -NoProfile -File framework/templates/Test-XiHanTemplates.ps1
```

脚本在隔离目录中把框架与模板打包，安装到隔离模板库（`--debug:custom-hive`），用隔离的 `NUGET_PACKAGES` 与 `NuGet.Config` 还原；逐个场景生成、Release 构建、测试，并以随机端口启动 Web 应用请求示例端点，最后卸载模板。不改动全局 NuGet 配置，不在用户模板库中安装或卸载模板。框架已按同一配置构建过时可加 `-NoFrameworkBuild`，排查问题时加 `-KeepWorkRoot` 保留工作目录与日志。

## 目录结构

```text
templates/
  XiHan.Framework.Templates.csproj   模板包项目（打包时把模板默认框架版本写成包版本）
  Test-XiHanTemplates.ps1            生成产物验证脚本
  README.md
  content/
    xihan-web/                       Web API 应用模板
    xihan-module/                    模块模板
```
