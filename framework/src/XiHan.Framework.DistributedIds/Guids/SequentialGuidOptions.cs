// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 顺序 GUID 选项
/// </summary>
public class SequentialGuidOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:DistributedIds:SequentialGuid";

    /// <summary>
    /// 默认顺序 GUID 类型
    /// </summary>
    public SequentialGuidType? DefaultSequentialGuidType { get; set; }

    /// <summary>
    /// 创建字符串形式的顺序 GUID 选项（适合字符串比较排序）
    /// </summary>
    /// <returns>顺序 GUID 选项</returns>
    public static SequentialGuidOptions AsString()
    {
        return new SequentialGuidOptions
        {
            DefaultSequentialGuidType = SequentialGuidType.SequentialAsString
        };
    }

    /// <summary>
    /// 创建二进制形式的顺序 GUID 选项（适合二进制排序）
    /// </summary>
    /// <returns>顺序 GUID 选项</returns>
    public static SequentialGuidOptions AsBinary()
    {
        return new SequentialGuidOptions
        {
            DefaultSequentialGuidType = SequentialGuidType.SequentialAsBinary
        };
    }

    /// <summary>
    /// 创建末尾形式的顺序 GUID 选项（推荐用于 SQL Server，适合聚集索引）
    /// </summary>
    /// <returns>顺序 GUID 选项</returns>
    public static SequentialGuidOptions AtEnd()
    {
        return new SequentialGuidOptions
        {
            DefaultSequentialGuidType = SequentialGuidType.SequentialAtEnd
        };
    }

    /// <summary>
    /// 创建默认的顺序 GUID 选项（末尾形式）
    /// </summary>
    /// <returns>顺序 GUID 选项</returns>
    public static SequentialGuidOptions Default()
    {
        return AtEnd();
    }

    /// <summary>
    /// 获取默认顺序 GUID 类型
    /// </summary>
    public SequentialGuidType GetDefaultSequentialGuidType() => DefaultSequentialGuidType ?? SequentialGuidType.SequentialAtEnd;
}
