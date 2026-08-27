// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XML 测试用示例人员
/// </summary>
/// <remarks>
/// XmlSerializer 要求类型公开且带无参构造，成员必须可读写，因此这里不使用 record 与只读属性。
/// </remarks>
public class XmlTestPerson
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
    /// 昵称（可空，用于验证 null 成员被省略后仍能还原为 null）
    /// </summary>
    public string? Nickname { get; set; }
}

/// <summary>
/// XML 测试用示例团队，含集合成员
/// </summary>
public class XmlTestTeam
{
    /// <summary>
    /// 团队名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 团队成员
    /// </summary>
    public List<XmlTestPerson> Members { get; set; } = [];
}
