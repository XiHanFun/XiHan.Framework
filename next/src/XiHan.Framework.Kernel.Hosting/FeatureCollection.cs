// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性集合，管理所有已注册的 IXiHanFeature。
/// </summary>
public sealed class FeatureCollection : IEnumerable<IXiHanFeature>
{
    private readonly List<IXiHanFeature> _features = [];

    /// <summary>
    /// 已注册的特性数量。
    /// </summary>
    public int Count => _features.Count;

    /// <summary>
    /// 注册一个特性。
    /// </summary>
    public FeatureCollection Add<TFeature>() where TFeature : IXiHanFeature, new()
        => Add(new TFeature());

    /// <summary>
    /// 注册一个特性实例。
    /// </summary>
    public FeatureCollection Add(IXiHanFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// 获取指定类型的特性。如果未注册则返回 null。
    /// </summary>
    public TFeature? Get<TFeature>() where TFeature : IXiHanFeature
        => _features.OfType<TFeature>().FirstOrDefault();

    /// <summary>
    /// 获取所有已注册的特性。
    /// </summary>
    public IReadOnlyList<IXiHanFeature> GetAll() => _features.AsReadOnly();

    IEnumerator<IXiHanFeature> IEnumerable<IXiHanFeature>.GetEnumerator() => _features.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _features.GetEnumerator();
}
