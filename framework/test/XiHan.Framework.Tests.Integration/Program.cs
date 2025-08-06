using XiHan.Framework.Console.Test;
using XiHan.Framework.Core.Application;

var app = await XiHanApplicationFactory.CreateAsync<XiHanTestsIntegrationModule>();
await app.InitializeAsync();
