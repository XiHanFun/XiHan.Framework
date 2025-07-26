#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkDocumentationAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架文档特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public class FrameworkDocumentationAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="title">文档标题</param>
    /// <param name="description">文档描述</param>
    /// <param name="tags">文档标签</param>
    /// <param name="exampleCode">示例代码</param>
    public FrameworkDocumentationAttribute(string title, string description = "", string[]? tags = null, string exampleCode = "")
    {
        Title = title;
        Description = description;
        Tags = tags ?? [];
        ExampleCode = exampleCode;
    }

    /// <summary>
    /// 文档标题
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 文档描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 文档标签
    /// </summary>
    public string[] Tags { get; }

    /// <summary>
    /// 示例代码
    /// </summary>
    public string ExampleCode { get; }
}
