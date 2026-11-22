// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// <c>MapXiHanOpenApi</c> 的文档选项
/// </summary>
/// <remarks>
/// 标题缺省取应用名，版本缺省取框架版本，<c>servers</c> 缺省不写，没有分组时只有一份全量文档
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiOptions
{
    /// <summary>
    /// 文档标题（<c>info.title</c>）
    /// </summary>
    /// <remarks>
    /// 为空取宿主的应用名
    /// </remarks>
    public string? Title
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The title must not be blank.", nameof(value));
    }

    /// <summary>
    /// 文档版本（<c>info.version</c>）
    /// </summary>
    /// <remarks>
    /// 为空取框架版本
    /// </remarks>
    public string? Version
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The version must not be blank.", nameof(value));
    }

    /// <summary>
    /// <c>servers</c>
    /// </summary>
    /// <remarks>
    /// 为空列表不写
    /// </remarks>
    public IReadOnlyList<OpenApiServer> Servers
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 分组
    /// </summary>
    /// <remarks>
    /// 为空列表时只有全量文档
    /// 组名唯一，不能是 <c>all</c>（全量文档的名字）；一个特性只能属于一个组
    /// </remarks>
    public IReadOnlyList<OpenApiGroup> Groups
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = Validated(value, nameof(value));
        }
    } = [];

    /// <summary>
    /// 核对一组分组并复制
    /// </summary>
    /// <remarks>
    /// 组名唯一且不是 <c>all</c>（全量文档的名字），一个特性只属于一个组；选项与运行期的分组来源（<see cref="IOpenApiGroupSource"/>）合在一起时用同一套规则
    /// </remarks>
    /// <param name="groups">分组</param>
    /// <param name="paramName">报错时的参数名</param>
    /// <exception cref="ArgumentException">分组里有 <see langword="null"/>、组名重复或保留、一个特性属于多个组</exception>
    internal static IReadOnlyList<OpenApiGroup> Validated(IEnumerable<OpenApiGroup> groups, string paramName)
    {
        var copied = groups.ToList();
        var names = new HashSet<string>(StringComparer.Ordinal) { OpenApiEndpointRouteBuilderExtensions.FullDocumentName };
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in copied)
        {
            ArgumentNullException.ThrowIfNull(group, paramName);
            if (!names.Add(group.Name))
            {
                throw new ArgumentException($"The OpenAPI group name '{group.Name}' is reserved or used twice.", paramName);
            }

            foreach (var owner in group.Owners)
            {
                if (!owners.Add(owner))
                {
                    throw new ArgumentException($"Feature '{owner}' belongs to more than one OpenAPI group.", paramName);
                }
            }
        }

        return copied;
    }
}
