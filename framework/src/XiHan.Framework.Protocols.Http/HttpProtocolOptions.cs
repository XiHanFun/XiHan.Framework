// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Http.Routing;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Protocols:Http</c> 显式读取：<c>RoutePrefix</c>，<c>Routes:&lt;操作 ID&gt;</c> = <c>METHOD /template</c> 显式覆盖推导的路由
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HttpProtocolOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:Http";

    /// <summary>
    /// 操作请求体的缺省上限
    /// </summary>
    /// <remarks>
    /// 4 MiB
    /// </remarks>
    public const long DefaultMaxRequestBodyBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 操作请求体的上限（字节）
    /// </summary>
    /// <remarks>
    /// 超过即 <see cref="HttpErrors.RequestBodyTooLarge"/>，缺省 <see cref="DefaultMaxRequestBodyBytes"/>；只管操作端点，宿主自己映射的端点照 Kestrel 的限制（ADR-0176）
    /// </remarks>
    public long MaxRequestBodyBytes
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum request body size must be positive.");
    } = DefaultMaxRequestBodyBytes;

    /// <summary>
    /// 所有操作路由的前缀（如 <c>/api</c>）
    /// </summary>
    /// <remarks>
    /// 空表示无前缀。不能以 <c>/</c> 结尾
    /// </remarks>
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
    /// 显式覆盖
    /// </summary>
    /// <remarks>
    /// 操作 ID → 路由；未覆盖的操作按 <see cref="HttpRouteConvention"/> 推导
    /// </remarks>
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
    /// 从配置读取
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

        var maxBody = DefaultMaxRequestBodyBytes;
        if (section["MaxRequestBodyBytes"] is { } maxText && !long.TryParse(maxText, NumberStyles.None, CultureInfo.InvariantCulture, out maxBody))
        {
            throw new ArgumentException($"{ConfigurationSection}:MaxRequestBodyBytes must be a positive integer, got '{maxText}'.");
        }

        return new HttpProtocolOptions
        {
            RoutePrefix = section["RoutePrefix"] ?? string.Empty,
            Routes = routes,
            MaxRequestBodyBytes = maxBody,
        };
    }

    /// <summary>
    /// 操作最终的路由
    /// </summary>
    /// <remarks>
    /// 显式覆盖优先，否则按约定推导；都加上前缀
    /// </remarks>
    public HttpRoute RouteFor(XiHan.Framework.Kernel.Operations.OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var route = Routes.TryGetValue(descriptor.Id, out var overridden) ? overridden : HttpRouteConvention.Derive(descriptor);
        return RoutePrefix.Length == 0 ? route : new HttpRoute(route.Method, RoutePrefix + route.Template);
    }
}
