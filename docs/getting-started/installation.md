# 2.1 环境与安装

这一页说明使用 XiHan Framework 需要什么环境，以及三种取得框架包的方式。读完它，你的机器就准备好了，可以进入[第一个应用](/getting-started/first-app)。

## 环境要求

| 项 | 要求 |
| --- | --- |
| .NET SDK | .NET 10 SDK。从源码构建框架时，用仓库 `global.json` 锁定的功能带 |
| 编辑器 | Visual Studio、JetBrains Rider 或 VS Code（C# Dev Kit）任选 |
| Git | 从源码引用或验证未发布改动时需要 |
| 外部设施 | 不需要。默认实现零外部依赖，数据库、Redis、消息代理在换 Provider 时才需要 |

检查 SDK：

```bash
dotnet --version
```

输出以 `10.` 开头即可。

## 框架处于预览阶段

当前所有包都是预览版，版本号带 `-preview.N` 后缀，安装时要带 `--prerelease`。公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整。正式版发布后，去掉 `--prerelease` 即可。

## 方式一：从 NuGet 安装

适合只使用框架、不改框架源码的场景，也是推荐方式。一个最小的 Web 应用需要三个包：

```bash
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
dotnet add package XiHan.Framework.Protocols.Http --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

| 包 | 作用 |
| --- | --- |
| `Profiles.Minimal` | 组合包：原语层、内核、ASP.NET Core 宿主、内存本地化 |
| `Protocols.Http` | HTTP 协议适配器：把操作契约映射为路由并发布 OpenAPI |
| `Kernel.Generators` | 源生成器：从 `[Operation]` 生成登记代码与 JSON Schema，只在编译期运行 |

`Kernel.Generators` 是开发期依赖：包里只有分析器资产，引用后在编译期运行，不进入应用的运行时依赖。手写项目文件时建议加 `PrivateAssets="all"`，避免它随你的包传递给下游：

```xml
<PackageReference Include="XiHan.Framework.Kernel.Generators" Version="<版本>" PrivateAssets="all" />
```

## 方式二：源码引用

适合同时调试框架源码的场景。

1. 克隆仓库。日常开发在 `dev` 分支：

   ```bash
   git clone --branch dev https://github.com/XiHanFun/XiHan.Framework.git
   cd XiHan.Framework
   ```

2. 初始化规范子模块。`Plugins.Protocol` 从 `framework/spec/plugin/plugin.proto` 生成代码，构建整个解决方案时需要它：

   ```bash
   git submodule update --init framework/spec
   ```

3. 应用项目以 `ProjectReference` 引用 `framework/src` 下的项目，源生成器按分析器引用：

   ```xml
   <ItemGroup>
     <ProjectReference Include="../XiHan.Framework/framework/src/XiHan.Framework.Profiles.Minimal/XiHan.Framework.Profiles.Minimal.csproj" />
     <ProjectReference Include="../XiHan.Framework/framework/src/XiHan.Framework.Protocols.Http/XiHan.Framework.Protocols.Http.csproj" />
     <ProjectReference Include="../XiHan.Framework/framework/src/XiHan.Framework.Kernel.Generators/XiHan.Framework.Kernel.Generators.csproj"
                       OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

## 方式三：本地包源

适合验证还没发布的改动：把仓库打成包，放到本地目录作为包源。

1. 在仓库根还原、构建并打包（先按方式二的第 2 步初始化子模块）：

   ```bash
   dotnet restore framework/XiHan.Framework.slnx
   dotnet build framework/XiHan.Framework.slnx -c Release --no-restore
   dotnet pack framework/XiHan.Framework.slnx -c Release --no-build
   ```

   包写到 `framework/artifacts/packages/`，文件名形如 `XiHan.Framework.Kernel.<版本>.nupkg`。

2. 在应用的解决方案目录放一个 `nuget.config`，把这个目录登记为包源：

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="xihan-local" value="../XiHan.Framework/framework/artifacts/packages" />
     </packageSources>
   </configuration>
   ```

3. 在应用项目里按方式一的命令添加引用。

## 命令行工具

`xihan` 是一个 .NET 工具，用来转发应用的自省命令、生成部署产物、以 stdio 提供框架知识库给 AI 编码助手。按需安装：

```bash
dotnet tool install --global XiHan.Framework.Cli --prerelease
```

用法见 [xihan 工具](/agents/cli)。

## 常见问题

### 还原时报找不到包

预览包只在带 `--prerelease` 时可见。手写 `PackageReference` 时版本号要写完整的预览版本，不能只写大版本。

### 用源码引用时报 `plugin.proto` 不存在

没有初始化规范子模块。在仓库根执行 `git submodule update --init framework/spec`。只引用 `Profiles.Minimal` 与 `Protocols.Http` 的应用不会碰到这个问题，构建整个解决方案才需要。

### `dotnet test` 报不认识 `--solution`

仓库用 Microsoft.Testing.Platform 作为测试运行器，由 `global.json` 启用，命令是 `dotnet test --solution framework/XiHan.Framework.slnx`。不要改回 VSTest 形式。

## 下一步

- [第一个应用](/getting-started/first-app)：写一个操作并经 HTTP 调用它
- [组合包与选包](/getting-started/profiles)：Minimal、Standard、Full 各含什么
