#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSkillResult
// Guid:aeeb6d18-a113-4a16-aaa4-63cd1b046202
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 技能执行结果
/// </summary>
public class XiHanSkillResult : XiHanAiResult
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSkillResult()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="content">内容</param>
    /// <param name="contentType">内容类型</param>
    public XiHanSkillResult(bool isSuccess, string content, string contentType = "text/plain") : base(isSuccess)
    {
        Content = content;
        ContentType = contentType;
    }

    /// <summary>
    /// 结果内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 结果类型
    /// </summary>
    public string ContentType { get; set; } = "text/plain";

    /// <summary>
    /// 相关元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="content">结果内容</param>
    /// <param name="contentType">结果类型</param>
    /// <returns>成功结果</returns>
    public static XiHanSkillResult Success(string content, string contentType = "text/plain")
    {
        return new XiHanSkillResult(true, content, contentType);
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>失败结果</returns>
    public static XiHanSkillResult Failure(string errorMessage)
    {
        var result = new XiHanSkillResult(false, string.Empty)
        {
            ErrorMessage = errorMessage
        };
        return result;
    }
}
