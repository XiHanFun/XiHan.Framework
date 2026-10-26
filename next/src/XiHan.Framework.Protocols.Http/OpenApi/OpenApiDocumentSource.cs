// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 生成一份 OpenAPI 文档的全部输入：操作登记表、HTTP 选项、权限码注册表，以及标题 / 版本 / <c>servers</c>、已装配的认证方案、要收录的普通端点与按区域取操作摘要的函数。
/// </summary>
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
    /// 文档标题；缺省是框架名。
    /// </summary>
    public string Title
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The title must not be blank.", nameof(value));
    } = FrameworkInfo.Name;

    /// <summary>
    /// 文档版本；缺省是框架版本。
    /// </summary>
    public string Version
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The version must not be blank.", nameof(value));
    } = FrameworkInfo.Version;

    /// <summary>
    /// <c>servers</c>；空列表不写。
    /// </summary>
    public IReadOnlyList<OpenApiServer> Servers
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 已装配的认证处理器的方案名；含 <c>Bearer</c> 时发布 <c>components.securitySchemes.bearer</c> 与全局 <c>security</c>。
    /// </summary>
    public IReadOnlyCollection<string> AuthenticationSchemes
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 候选的普通端点（通常是宿主全部 <c>EndpointDataSource</c> 的端点）；按收录规则筛选，操作端点与没有描述元数据的端点不进文档。
    /// </summary>
    public IReadOnlyList<Endpoint> Endpoints
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 按操作 ID 取摘要（消息目录里 <c>&lt;操作 ID&gt;.description</c> 的区域文案）；为 <see langword="null"/> 或返回 <see langword="null"/> 时不写 <c>summary</c>。
    /// </summary>
    public Func<string, string?>? Describe { get; init; }
}
