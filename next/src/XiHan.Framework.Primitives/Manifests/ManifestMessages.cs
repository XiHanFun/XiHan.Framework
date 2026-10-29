// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.


namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单校验的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class ManifestMessages
{
    /// <summary>
    /// 插件清单必须声明 <c>isolation</c>。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string IsolationRequired = "primitives.manifest.isolation_required";

    /// <summary>
    /// 非插件清单不得声明 <c>isolation</c> 或 <c>permissions</c>。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string IsolationNotAllowed = "primitives.manifest.isolation_not_allowed";

    /// <summary>
    /// <c>provides</c> 里能力 ID 重复。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string DuplicateProvides = "primitives.manifest.duplicate_provides";

    /// <summary>
    /// <c>requires</c> 里能力 ID 重复。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string DuplicateRequires = "primitives.manifest.duplicate_requires";

    /// <summary>
    /// <c>bindings</c> 里绑定名重复。
    /// </summary>
    /// <remarks>
    /// 参数：<c>id</c>。
    /// </remarks>
    public const string DuplicateBindings = "primitives.manifest.duplicate_bindings";
}
