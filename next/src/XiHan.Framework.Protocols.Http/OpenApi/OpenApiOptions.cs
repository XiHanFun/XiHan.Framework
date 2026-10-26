// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// <c>MapXiHanOpenApi</c> 的文档选项：标题、版本与 <c>servers</c>。标题缺省取应用名，版本缺省取框架版本，<c>servers</c> 缺省不写。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiOptions
{
    /// <summary>
    /// 文档标题（<c>info.title</c>）；为空取宿主的应用名。
    /// </summary>
    public string? Title
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The title must not be blank.", nameof(value));
    }

    /// <summary>
    /// 文档版本（<c>info.version</c>）；为空取框架版本。
    /// </summary>
    public string? Version
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The version must not be blank.", nameof(value));
    }

    /// <summary>
    /// <c>servers</c>；为空列表不写。
    /// </summary>
    public IReadOnlyList<OpenApiServer> Servers
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];
}
