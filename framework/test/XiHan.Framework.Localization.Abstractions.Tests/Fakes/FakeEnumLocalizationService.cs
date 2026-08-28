// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Localization.Abstractions.Enums;

namespace XiHan.Framework.Localization.Abstractions.Tests.Fakes;

/// <summary>
/// 手写的枚举本地化服务替身
/// </summary>
/// <remarks>
/// 抽象包只定义接口，没有实现。该替身用于锁定接口的可实现形状：
/// 两个同名 Get 重载不冲突、可选参数默认值为 null、TryGet 的 out 结果可为 null。
/// </remarks>
public sealed class FakeEnumLocalizationService : IEnumLocalizationService
{
    private readonly Dictionary<string, LocalizedEnumDefinition> _definitions;

    /// <summary>
    /// 初始化枚举本地化服务替身
    /// </summary>
    /// <param name="definitions">预置的枚举描述，键为枚举类型名</param>
    public FakeEnumLocalizationService(IDictionary<string, LocalizedEnumDefinition>? definitions = null)
    {
        _definitions = definitions is null
            ? new Dictionary<string, LocalizedEnumDefinition>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, LocalizedEnumDefinition>(definitions, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 记录每次单项读取收到的查询参数（按调用顺序，可能为 null）
    /// </summary>
    public List<EnumLocalizationQuery?> ReceivedQueries { get; } = [];

    /// <summary>
    /// 按类型读取枚举本地化描述
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="query">查询参数</param>
    /// <returns>枚举本地化描述</returns>
    public LocalizedEnumDefinition Get(Type enumType, EnumLocalizationQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        return Get(enumType.Name, query);
    }

    /// <summary>
    /// 按名称读取枚举本地化描述
    /// </summary>
    /// <param name="enumTypeName">枚举类型名</param>
    /// <param name="query">查询参数</param>
    /// <returns>枚举本地化描述</returns>
    public LocalizedEnumDefinition Get(string enumTypeName, EnumLocalizationQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(enumTypeName);
        ReceivedQueries.Add(query);
        return _definitions.TryGetValue(enumTypeName, out var definition)
            ? definition
            : throw new KeyNotFoundException(enumTypeName);
    }

    /// <summary>
    /// 批量读取枚举本地化描述，未登记的类型名直接跳过
    /// </summary>
    /// <param name="enumTypeNames">枚举类型名列表</param>
    /// <param name="query">查询参数</param>
    /// <returns>键为类型名的本地化结果</returns>
    public IReadOnlyDictionary<string, LocalizedEnumDefinition> GetMany(
        IEnumerable<string> enumTypeNames,
        EnumLocalizationQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(enumTypeNames);

        var result = new Dictionary<string, LocalizedEnumDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var enumTypeName in enumTypeNames)
        {
            if (_definitions.TryGetValue(enumTypeName, out var definition))
            {
                result[enumTypeName] = definition;
            }
        }

        return result;
    }

    /// <summary>
    /// 尝试按名称读取枚举本地化描述
    /// </summary>
    /// <param name="enumTypeName">枚举类型名</param>
    /// <param name="result">读取结果</param>
    /// <param name="query">查询参数</param>
    /// <returns>是否读取成功</returns>
    public bool TryGet(
        string enumTypeName,
        out LocalizedEnumDefinition? result,
        EnumLocalizationQuery? query = null)
    {
        ReceivedQueries.Add(query);
        return _definitions.TryGetValue(enumTypeName, out result);
    }
}
