// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions.Tests.Fakes;

/// <summary>
/// 手写的可本地化字符串替身
/// </summary>
/// <remarks>
/// 直接返回预置的本地化结果，用于精确控制 ResourceNotFound 分支，验证扩展方法的回退取舍。
/// </remarks>
public sealed class FakeLocalizableString : ILocalizableString
{
    private readonly LocalizedString _result;

    /// <summary>
    /// 初始化可本地化字符串替身
    /// </summary>
    /// <param name="result">Localize 固定返回的结果</param>
    public FakeLocalizableString(LocalizedString result)
    {
        _result = result;
    }

    /// <summary>
    /// Localize 被调用的次数
    /// </summary>
    public int LocalizeCallCount { get; private set; }

    /// <summary>
    /// 最后一次 Localize 收到的工厂实例
    /// </summary>
    public IStringLocalizerFactory? LastFactory { get; private set; }

    /// <summary>
    /// 创建一个"资源已命中"的替身
    /// </summary>
    /// <param name="name">资源键</param>
    /// <param name="value">本地化文本</param>
    /// <returns>替身实例</returns>
    public static FakeLocalizableString Found(string name, string value)
    {
        return new FakeLocalizableString(new LocalizedString(name, value, resourceNotFound: false));
    }

    /// <summary>
    /// 创建一个"资源缺失"的替身
    /// </summary>
    /// <param name="name">资源键</param>
    /// <returns>替身实例</returns>
    public static FakeLocalizableString Missing(string name)
    {
        return new FakeLocalizableString(new LocalizedString(name, name, resourceNotFound: true));
    }

    /// <summary>
    /// 返回预置的本地化结果
    /// </summary>
    /// <param name="stringLocalizerFactory">本地化工厂</param>
    /// <returns>预置结果</returns>
    public LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)
    {
        LocalizeCallCount++;
        LastFactory = stringLocalizerFactory;
        return _result;
    }
}
