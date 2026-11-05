// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;
using SimpleCli;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp;

// 命令行宿主：同一份操作契约既是命令（greetings create --name Ada），也是 MCP tool（mcp 命令的 stdio 传输）
var builder = Host.CreateApplicationBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(CliMessages)));
builder.Services.AddXiHanCommandLine();

return await builder.Build().RunXiHanCommandLineAsync(args);
