// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器选项。从配置节 <c>XiHan:Protocols:Http</c> 显式读取：<c>RoutePrefix</c>，<c>Routes:&lt;操作 ID&gt;</c> = <c>METHOD /template</c> 显式覆盖推导的路由。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HttpProtocolOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:Http";

    /// <summary>
    /// 所有操作路由的前缀（如 <c>/api</c>）；空表示无前缀。不能以 <c>/</c> 结尾。
    /// </summary>
    public string RoutePrefix
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 0 && (!value.StartsWith('/') || value.EndsWith('/')))
            {
                throw new ArgumentException($"Route prefix '{value}' must start with '/' and must not end with '/'.", nameof(value));
            }

            field = value;
        }
    } = string.Empty;

    /// <summary>
    /// 显式覆盖：操作 ID → 路由；未覆盖的操作按 <see cref="HttpRouteConvention"/> 推导。
    /// </summary>
    public IReadOnlyDictionary<string, HttpRoute> Routes
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var id in value.Keys)
            {
                OperationId.Validate(id);
            }

            field = value;
        }
    } = new Dictionary<string, HttpRoute>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static HttpProtocolOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var routes = new Dictionary<string, HttpRoute>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Routes").GetChildren())
        {
            if (child.Value is { } text)
            {
                routes[child.Key] = HttpRoute.Parse(text);
            }
        }

        return new HttpProtocolOptions
        {
            RoutePrefix = section["RoutePrefix"] ?? string.Empty,
            Routes = routes,
        };
    }

    /// <summary>
    /// 操作最终的路由：显式覆盖优先，否则按约定推导；都加上前缀。
    /// </summary>
    public HttpRoute RouteFor(XiHan.Framework.Kernel.Operations.OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var route = Routes.TryGetValue(contract.Id, out var overridden) ? overridden : HttpRouteConvention.Derive(contract);
        return RoutePrefix.Length == 0 ? route : new HttpRoute(route.Method, RoutePrefix + route.Template);
    }
}
