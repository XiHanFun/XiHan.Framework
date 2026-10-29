// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 生成一份 OpenAPI 文档的全部输入。
/// </summary>
/// <remarks>
/// 操作登记表、HTTP 选项、权限码注册表，以及标题 / 版本 / <c>servers</c>、已装配的认证方案、要收录的普通端点、分组与按键取文档区域文案的函数。
/// <see cref="FromServices"/> 从容器组装这些输入，<c>MapXiHanOpenApi</c> 与导出工具（如 <c>contracts export</c>）共用它，产出同一份文档。
/// </remarks>
/// <param name="Registry">操作登记表。</param>
/// <param name="Options">HTTP 选项（路由覆盖与前缀）。</param>
/// <param name="Permissions">权限码注册表；为 <see langword="null"/> 时 <c>x-xihan-permissions</c> 只收录被引用的权限码，且只有 <c>code</c>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiDocumentSource(OperationRegistry Registry, HttpProtocolOptions Options, PermissionRegistry? Permissions)
{
    /// <summary>
    /// 操作登记表。
    /// </summary>
    public OperationRegistry Registry { get; } = Registry ?? throw new ArgumentNullException(nameof(Registry));

    /// <summary>
    /// HTTP 选项。
    /// </summary>
    public HttpProtocolOptions Options { get; } = Options ?? throw new ArgumentNullException(nameof(Options));

    /// <summary>
    /// 文档标题。
    /// </summary>
    /// <remarks>
    /// 缺省是框架名。
    /// </remarks>
    public string Title
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The title must not be blank.", nameof(value));
    } = FrameworkInfo.Name;

    /// <summary>
    /// 文档版本。
    /// </summary>
    /// <remarks>
    /// 缺省是框架版本。
    /// </remarks>
    public string Version
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The version must not be blank.", nameof(value));
    } = FrameworkInfo.Version;

    /// <summary>
    /// <c>servers</c>。
    /// </summary>
    /// <remarks>
    /// 空列表不写。
    /// </remarks>
    public IReadOnlyList<OpenApiServer> Servers
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 已装配的认证处理器的方案名。
    /// </summary>
    /// <remarks>
    /// 含 <c>Bearer</c> 时发布 <c>components.securitySchemes.bearer</c> 与全局 <c>security</c>。
    /// </remarks>
    public IReadOnlyCollection<string> AuthenticationSchemes
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 候选的普通端点（通常是宿主全部 <c>EndpointDataSource</c> 的端点）。
    /// </summary>
    /// <remarks>
    /// 按收录规则筛选，操作端点与没有描述元数据的端点不进文档。
    /// </remarks>
    public IReadOnlyList<Endpoint> Endpoints
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 按消息键（与参数）取文档区域的文案。
    /// </summary>
    /// <remarks>
    /// 返回 <see langword="null"/> 表示没有这条文案；为 <see langword="null"/> 时文档不带任何目录文案（操作标题与说明、标签与分组的显示名、字段说明的区域覆盖、授权要求行）。
    /// </remarks>
    public Func<string, IReadOnlyDictionary<string, object?>?, string?>? Localize { get; init; }

    /// <summary>
    /// 分组。
    /// </summary>
    /// <remarks>
    /// 为空列表时没有 <c>x-tagGroups</c>。
    /// </remarks>
    public IReadOnlyList<OpenApiGroup> Groups
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 只出这一组的文档。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时出全量文档。
    /// 必须是 <see cref="Groups"/> 里的组名，否则生成时抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public string? Group { get; init; }

    /// <summary>
    /// 从容器组装文档输入。
    /// </summary>
    /// <remarks>
    /// 操作登记表（必须已登记）、HTTP 选项（没有则缺省）、权限码注册表、已装配认证处理器的方案名；标题 / 版本 / <c>servers</c> / 分组取 <paramref name="options"/>（标题缺省取宿主应用名、版本缺省取框架版本）。
    /// 装配了本地化时按 <paramref name="locale"/>（为 <see langword="null"/> 取本地化的默认区域）从消息目录取文案，没有装配则不带目录文案。
    /// </remarks>
    /// <param name="services">应用容器（装配完成后）。</param>
    /// <param name="options">文档选项；为 <see langword="null"/> 按缺省。</param>
    /// <param name="endpoints">候选的普通端点，通常是宿主全部 <c>EndpointDataSource</c> 的端点。</param>
    /// <param name="locale">文案的区域。</param>
    /// <param name="group">只出这一组；为 <see langword="null"/> 出全量文档。</param>
    public static OpenApiDocumentSource FromServices(IServiceProvider services, OpenApiOptions? options, IEnumerable<Endpoint> endpoints, string? locale = null, string? group = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoints);
        options ??= new OpenApiOptions();
        var localizer = services.GetService<IMessageLocalizer>();
        return new OpenApiDocumentSource(
            services.GetRequiredService<OperationRegistry>(),
            services.GetService<HttpProtocolOptions>() ?? new HttpProtocolOptions(),
            services.GetService<PermissionRegistry>())
        {
            Title = options.Title ?? services.GetService<IHostEnvironment>()?.ApplicationName ?? FrameworkInfo.Name,
            Version = options.Version ?? FrameworkInfo.Version,
            Servers = options.Servers,
            AuthenticationSchemes = services.GetServices<IAuthenticationHandler>().Select(static h => h.Scheme).ToList(),
            Endpoints = endpoints.ToList(),
            Groups = options.Groups,
            Group = group,
            Localize = localizer is null ? null : LocalizerOf(localizer, locale ?? localizer.Options.DefaultLocale),
        };
    }

    /// <summary>
    /// 按键取某个区域的文案：键不合文法或目录里没有就不给。
    /// </summary>
    private static Func<string, IReadOnlyDictionary<string, object?>?, string?> LocalizerOf(IMessageLocalizer localizer, string locale)
        => (key, arguments) => MessageTemplate.IsValidKey(key) && localizer.Resolve(key, locale, arguments).TryGetValue(out var text) ? text.Text : null;
}
