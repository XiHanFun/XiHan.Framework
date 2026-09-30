using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using XiHanWebApp;

var builder = WebApplication.CreateBuilder(args);

// 以启动模块为根装配整棵模块依赖树
await builder.AddApplicationAsync<WebAppHostModule>();

var app = builder.Build();

// 触发各模块的初始化钩子，装配中间件管道与端点
await app.InitializeApplicationAsync();

await app.RunAsync();

/// <summary>
/// 程序入口（集成测试经 WebApplicationFactory 引用）
/// </summary>
public partial class Program;
