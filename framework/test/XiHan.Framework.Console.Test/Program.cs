using XiHan.Framework.Console.Test;
using XiHan.Framework.Core.Application;

var application = await XiHanApplicationFactory.CreateAsync<XiHanConsoleTestModule>();
await application.InitializeAsync();
