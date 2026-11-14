// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 代数缓存里一个条目的位置：范围与范围下的条目
/// </summary>
/// <remarks>
/// <see cref="Scope"/> 是失效的单位（如授予缓存的主体、租户目录的租户），<see cref="Entry"/> 区分同一范围下的条目（如授予缓存按租户分条目），范围只有一个条目时用空字符串。两者可以是任意文本：<see cref="GenerationCache{T}"/> 按长度前缀拼进缓存键，含空白、控制字符或过长的部分换成摘要
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct GenerationCacheKey
{
    /// <summary>
    /// 范围
    /// </summary>
    private readonly string? _scope;

    /// <summary>
    /// 条目
    /// </summary>
    private readonly string? _entry;

    /// <summary>
    /// 创建
    /// </summary>
    public GenerationCacheKey(string scope, string entry)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>
    /// 范围：按它失效
    /// </summary>
    public string Scope => _scope ?? throw new InvalidOperationException("The 'GenerationCacheKey' is the default value and has no scope; create it with the constructor.");

    /// <summary>
    /// 范围下的条目
    /// </summary>
    public string Entry => _entry ?? throw new InvalidOperationException("The 'GenerationCacheKey' is the default value and has no entry; create it with the constructor.");

    /// <summary>
    /// 范围只有一个条目时的位置
    /// </summary>
    public static GenerationCacheKey ForScope(string scope) => new(scope, string.Empty);
}
