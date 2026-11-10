# XiHan.Framework.Hosting.AspNetCore

## 关于

`XiHan.Framework.Hosting.AspNetCore` 是 XiHan 的 ASP.NET Core 宿主适配：把每个 HTTP 请求翻译成调用上下文并经内核管道执行，映射健康探针，失败时按 RFC 9457 写 Problem Details。所有 Web 形态的 XiHan 应用都用它承载。

## 主要特性

* `UseXiHan()` 把请求接入内核管道，支持 `traceparent`、`baggage`、`Accept-Language` 与截止时间
* `MapXiHanHealth()` 提供 startup / ready / live 探针
* 端点用 `RequireXiHanPermission` 等声明授权要求，启动时核对
* 认证方案 `XiHan` 供 SignalR、SSE 等绕过管道的端点使用
* 可挂载配置目录 `XIHAN_CONFIG_PATH`
* 启动时打印启动 Banner（Logo、框架与应用版本、环境、运行时），应用版本取自入口程序集，`XiHan:Kernel:Banner:Enabled=false` 关闭

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Hosting.AspNetCore --prerelease
```

### 使用

`Profiles.*` 已引用本包。在 `WebApplication` 里启用：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

## 主要类型

此库提供的主要类型：

* `UseXiHan`、`MapXiHanHealth`：中间件与探针端点
* `HttpInvocation`：`GetXiHanContext()` 取当前调用上下文
* `ProblemResponse`：Problem Details 响应体
* `RequireXiHanPermission`、`RequireXiHanAuthentication`、`RequireXiHanPolicy`：端点授权要求
* `HostedBlobUrlFeature`、`MapXiHanBlobs`：文件系统存储的预签名地址

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/hosting-aspnetcore)

## 相关包

* 命令行宿主：[XiHan.Framework.Hosting.CommandLine](https://www.nuget.org/packages/XiHan.Framework.Hosting.CommandLine)
* 最小组合：[XiHan.Framework.Profiles.Minimal](https://www.nuget.org/packages/XiHan.Framework.Profiles.Minimal)
* 标准组合：[XiHan.Framework.Profiles.Standard](https://www.nuget.org/packages/XiHan.Framework.Profiles.Standard)

## 反馈与贡献

`XiHan.Framework.Hosting.AspNetCore` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
