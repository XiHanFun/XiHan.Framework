// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Localization.Abstractions.Enums;

/// <summary>
/// 枚举本地化服务
/// 提供按类型、按名称及批量方式读取本地化枚举描述
/// </summary>
public interface IEnumLocalizationService
{
    /// <summary>
    /// 按类型读取枚举本地化描述
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="query">查询参数</param>
    /// <returns>枚举本地化描述</returns>
    LocalizedEnumDefinition Get(Type enumType, EnumLocalizationQuery? query = null);

    /// <summary>
    /// 按名称读取枚举本地化描述
    /// </summary>
    /// <param name="enumTypeName">枚举类型名（支持短名或完整名）</param>
    /// <param name="query">查询参数</param>
    /// <returns>枚举本地化描述</returns>
    LocalizedEnumDefinition Get(string enumTypeName, EnumLocalizationQuery? query = null);

    /// <summary>
    /// 批量读取枚举本地化描述
    /// </summary>
    /// <param name="enumTypeNames">枚举类型名列表</param>
    /// <param name="query">查询参数</param>
    /// <returns>键为类型名的本地化结果</returns>
    IReadOnlyDictionary<string, LocalizedEnumDefinition> GetMany(
        IEnumerable<string> enumTypeNames,
        EnumLocalizationQuery? query = null);

    /// <summary>
    /// 尝试按名称读取枚举本地化描述
    /// </summary>
    /// <param name="enumTypeName">枚举类型名（支持短名或完整名）</param>
    /// <param name="result">读取结果</param>
    /// <param name="query">查询参数</param>
    /// <returns>是否读取成功</returns>
    bool TryGet(
        string enumTypeName,
        out LocalizedEnumDefinition? result,
        EnumLocalizationQuery? query = null);
}
