// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using XiHan.Framework.Localization.Abstractions;

namespace XiHan.Framework.ObjectMapping.Tests.Fakes;

/// <summary>
/// 固定文本的可本地化字符串替身
/// </summary>
/// <remarks>
/// 只用于验证 ObjectExtensionPropertyInfo.DisplayName 的读写契约，不接入真实本地化管线。
/// </remarks>
public sealed class FakeLocalizableString : ILocalizableString
{
    private readonly string _value;

    /// <summary>
    /// 初始化固定文本的可本地化字符串替身
    /// </summary>
    /// <param name="value">固定文本</param>
    public FakeLocalizableString(string value)
    {
        _value = value;
    }

    /// <summary>
    /// 原样返回构造时的固定文本
    /// </summary>
    /// <param name="stringLocalizerFactory">字符串本地化工厂</param>
    /// <returns>本地化结果</returns>
    public LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)
    {
        return new LocalizedString(_value, _value);
    }
}
