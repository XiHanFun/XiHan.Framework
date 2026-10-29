// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Inbound;
using XiHan.Framework.Protocols.Grpc.Services;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Mcp.Transports;

namespace XiHan.Framework.Profiles.Full.Tests;

/// <summary>
/// Full 组合：零外部基础设施下每个能力都选中默认实现，管道阶段齐全，四种协议的端点都能映射并应答。
/// </summary>
public sealed class FullProfileTests
{
    /// <summary>
    /// 每个清单在 Full 组合里应被选中的能力。
    /// </summary>
    private static readonly Dictionary<string, string[]> _expected = new(StringComparer.Ordinal)
    {
        ["xihan.localization.inmemory"] = ["localization.resources"],
        ["xihan.persistence.inmemory"] = ["persistence.relational"],
        ["xihan.caching.inmemory"] = ["caching.distributed", "caching.local"],
        ["xihan.multitenancy.inmemory"] = ["multitenancy.context", "multitenancy.sharding"],
        ["xihan.security.inmemory"] = ["security.authentication", "security.authorization"],
        ["xihan.messaging.inmemory"] = ["messaging.queue"],
        ["xihan.objectstorage.filesystem"] = ["objectstorage.blob", "objectstorage.multipart"],
        ["xihan.scheduling.inmemory"] = ["scheduling.jobs", "scheduling.leader"],
        ["xihan.auditing.inmemory"] = ["auditing.trail"],
        ["xihan.workflow.inmemory"] = ["workflow.execution"],
        ["xihan.ai.tools"] = ["ai.tools"],
        ["xihan.observability.opentelemetry"] = ["observability.export"],
        ["xihan.protocols.http"] = ["protocols.http"],
        ["xihan.protocols.grpc"] = ["protocols.grpc"],
        ["xihan.protocols.mcp"] = ["protocols.mcp"],
        ["xihan.protocols.cloudevents"] = ["protocols.cloudevents"],
    };

    [Fact]
    public async Task FullProfile_SelectsEveryDefault_AndServesAllProtocols()
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-full-" + Guid.NewGuid().ToString("N"));
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:ObjectStorage:FileSystem:RootPath"] = root });
            builder.AddXiHan(static xihan => xihan.UseFullProfile());

            await using var app = builder.Build();
            app.UseXiHan();
            app.MapXiHanHealth();
            app.MapXiHanOperations();
            app.MapXiHanOpenApi();
            app.MapXiHanGrpc();
            app.MapXiHanProto();
            app.MapXiHanMcp();
            app.MapXiHanCloudEvents();
            await app.StartAsync();

            var xihan = app.Services.GetRequiredService<App>();
            foreach (var (manifestId, capabilities) in _expected)
            {
                Assert.True(xihan.Graph.Contains(manifestId), $"'{manifestId}' is missing from the graph.");
                Assert.Equal(capabilities, xihan.Graph[manifestId].SelectedCapabilities.Order(StringComparer.Ordinal));
            }

            var pipeline = xihan.DescribePipeline();
            foreach (var middleware in new[] { "AuthenticationMiddleware", "AuthorizationMiddleware", "IdempotencyMiddleware", nameof(UnitOfWorkMiddleware), nameof(AuditMiddleware) })
            {
                Assert.Contains(middleware, pipeline, StringComparison.Ordinal);
            }

            using var client = app.GetTestClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
            var openApi = await client.GetAsync("/openapi.json");
            Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
            using (var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync()))
            {
                // 缺省标题是应用名、版本是框架版本；探针、MCP、proto 与文档端点自身没有描述元数据，不进文档；Full 组合的认证是 ApiKey，不发布 Bearer 方案
                var openApiRoot = document.RootElement;
                Assert.Equal(app.Environment.ApplicationName, openApiRoot.GetProperty("info").GetProperty("title").GetString());
                Assert.Equal(FrameworkInfo.Version, openApiRoot.GetProperty("info").GetProperty("version").GetString());
                Assert.Empty(openApiRoot.GetProperty("paths").EnumerateObject());
                Assert.False(openApiRoot.GetProperty("components").TryGetProperty("securitySchemes", out _));
                Assert.False(openApiRoot.TryGetProperty("security", out _));
                Assert.True(openApiRoot.GetProperty("components").GetProperty("schemas").TryGetProperty("ProblemDetails", out _));
            }

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/operations.proto")).StatusCode);
            using var tools = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json");
            var mcp = await client.PostAsync("/mcp", tools);
            Assert.Equal(HttpStatusCode.OK, mcp.StatusCode);
            Assert.Contains("\"tools\"", await mcp.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            await app.StopAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
