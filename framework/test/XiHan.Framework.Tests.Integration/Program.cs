using XiHan.Framework.Core.Application;
using XiHan.Framework.Tests.Integration;

var app = await XiHanApplicationFactory.CreateAsync<XiHanTestsIntegrationModule>();
await app.InitializeAsync();
