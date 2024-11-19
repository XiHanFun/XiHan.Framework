using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Test;

Console.WriteLine("Hello, World!");

// 初始化 ABP 应用
using var application = XiHanApplicationFactory.Create<XiHanFrameworkCoreTestModule>();
application.Initialize();

// 获取服务并执行逻辑
var logger = application.ServiceProvider.GetRequiredService<ILogger<Program>>();

var myService = application.ServiceProvider.GetRequiredService<MyService>();
await myService.RunAsync();

Console.WriteLine("Hello, World Shutdown!");
application.Shutdown();
