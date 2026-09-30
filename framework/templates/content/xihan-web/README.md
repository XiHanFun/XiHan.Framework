# XiHanWebApp

基于 XiHan.Framework 的 Web API 应用，由 `xihan-web` 模板生成。

## 运行

```bash
dotnet run --project src/XiHanWebApp
```

启动后访问：

- 接口文档：`/scalar`
- 示例接口：`GET /api/Hello/Greeting?name=XiHan`
<!--#if (Data) -->
- 待办接口：`POST /api/TodoItem/Create`、`GET /api/TodoItem/List`；SQLite 数据库文件 `webapphost-db.db` 位于启动时的工作目录
<!--#endif -->
<!--#if (MultiTenancy) -->
- 当前租户：`GET /api/Tenant/Current`，请求头 `X-Tenant-Id: demo` 或 `X-Tenant-Id: 1`；租户登记在 `appsettings.json` 的 `XiHan:MultiTenancy:DefaultStore:Tenants`
<!--#endif -->
<!--#if (Observability) -->
- 健康检查：`GET /health`；OpenTelemetry 配置见 `appsettings.json` 的 `XiHan:Observability`（默认关闭）
<!--#endif -->

## 测试

```bash
dotnet test
```

## 目录结构

```text
src/XiHanWebApp/          应用：Program.cs、启动模块 WebAppHostModule.cs、应用服务
test/XiHanWebApp.Tests/   集成测试（WebApplicationFactory + xUnit v3）
Directory.Build.props     XiHan.Framework 包版本（XiHanFrameworkVersion）与公共编译选项
global.json               .NET SDK 与测试运行器（Microsoft.Testing.Platform）
```

## 升级框架

修改 `Directory.Build.props` 中的 `XiHanFrameworkVersion` 后重新还原、构建。
