// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;

string repositoryRoot;
try
{
    repositoryRoot = DocSourceLocator.ResolveRepositoryRoot(
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("XIHAN_DOCS_ROOT"));
}
catch (DocsRootNotFoundException ex)
{
    // stdout 是 MCP 协议通道，错误信息只能走 stderr
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

// 所有日志强制写入 stderr：写入 stdout 会插进 JSON-RPC 流中破坏连接
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new DocsMcpOptions());
builder.Services.AddSingleton(new DocSourceLocator(repositoryRoot));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DocIndex>();
builder.Services.AddSingleton<SectionScorer>();
builder.Services.AddSingleton(provider => SynonymExpander.Load(
    Path.Combine(AppContext.BaseDirectory, "Resources", "synonyms.json"),
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<SynonymExpander>()));
builder.Services.AddSingleton<DocsMcpTools>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// 启动时同步建立索引，建完才开始接受请求
host.Services.GetRequiredService<DocIndex>().EnsureFresh();

await host.RunAsync();
return 0;
