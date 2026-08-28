// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions.Tests.Fakes;

/// <summary>
/// 手写的字符串本地化工厂替身
/// </summary>
/// <remarks>
/// 记录两个 Create 重载各自的入参，用于断言 ILocalizableString 实现走的是"按类型"还是"按资源名"取器路径。
/// </remarks>
public sealed class FakeStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// 初始化本地化工厂替身
    /// </summary>
    /// <param name="localizer">工厂固定返回的本地化器，为空时使用无词条的默认替身</param>
    public FakeStringLocalizerFactory(IStringLocalizer? localizer = null)
    {
        _localizer = localizer ?? new FakeStringLocalizer();
    }

    /// <summary>
    /// 记录按资源类型创建时收到的类型
    /// </summary>
    public List<Type> CreatedResourceTypes { get; } = [];

    /// <summary>
    /// 记录按资源名创建时收到的 baseName 与 location
    /// </summary>
    public List<(string BaseName, string Location)> CreatedResourceNames { get; } = [];

    /// <summary>
    /// 两个重载合计的调用次数
    /// </summary>
    public int CreateCallCount => CreatedResourceTypes.Count + CreatedResourceNames.Count;

    /// <summary>
    /// 按资源类型创建本地化器
    /// </summary>
    /// <param name="resourceSource">资源类型</param>
    /// <returns>本地化器</returns>
    public IStringLocalizer Create(Type resourceSource)
    {
        CreatedResourceTypes.Add(resourceSource);
        return _localizer;
    }

    /// <summary>
    /// 按资源名创建本地化器
    /// </summary>
    /// <param name="baseName">资源基名</param>
    /// <param name="location">资源位置</param>
    /// <returns>本地化器</returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        CreatedResourceNames.Add((baseName, location));
        return _localizer;
    }
}
