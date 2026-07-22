// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 可本地化字符串接口
/// 定义支持多语言本地化的字符串对象的基本契约
/// </summary>
public interface ILocalizableString
{
    /// <summary>
    /// 使用指定的字符串本地化工厂对字符串进行本地化处理
    /// </summary>
    /// <param name="stringLocalizerFactory">字符串本地化工厂实例</param>
    /// <returns>本地化后的字符串结果</returns>
    /// <exception cref="ArgumentNullException">当 stringLocalizerFactory 为 null 时</exception>
    LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory);
}
