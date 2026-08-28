// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JSON 测试用示例用户
/// </summary>
/// <remarks>
/// 刻意覆盖多种成员形态：普通值类型、可空引用、集合、嵌套对象、只读属性，
/// 以便一次模型同时验证命名策略、null 处理、只读属性忽略与嵌套往返。
/// </remarks>
public sealed class JsonSampleUser
{
    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 年龄
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// 是否启用（用于验证多单词属性的命名策略转换）
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 昵称（可空，用于验证 null 处理）
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// 标签集合
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// 地址（嵌套对象）
    /// </summary>
    public JsonSampleAddress? Address { get; set; }
}

/// <summary>
/// JSON 测试用示例地址
/// </summary>
public sealed class JsonSampleAddress
{
    /// <summary>
    /// 城市
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 国家
    /// </summary>
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// JSON 测试用自引用节点，用于构造循环引用与深层嵌套
/// </summary>
public sealed class JsonSampleNode
{
    /// <summary>
    /// 节点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 下一个节点
    /// </summary>
    public JsonSampleNode? Next { get; set; }
}

/// <summary>
/// JSON 测试用只读属性载体
/// </summary>
public sealed class JsonSampleReadOnlyHolder
{
    /// <summary>
    /// 可读写属性
    /// </summary>
    public string Writable { get; set; } = "写入值";

    /// <summary>
    /// 只读属性（无 setter，受 IgnoreReadOnlyProperties 控制）
    /// </summary>
    public string Computed => "计算值";
}

/// <summary>
/// JSON 测试用特殊字符载体
/// </summary>
public sealed class JsonSampleText
{
    /// <summary>
    /// 文本内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
