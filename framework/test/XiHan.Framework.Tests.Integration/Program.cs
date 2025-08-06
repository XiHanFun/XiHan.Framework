using XiHan.Framework.Console.Test;
using XiHan.Framework.Core.Application;

var app = await XiHanApplicationFactory.CreateAsync<XiHanConsoleTestModule>();
await app.InitializeAsync();
