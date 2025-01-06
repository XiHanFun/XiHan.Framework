#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSet
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:30:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件集合
/// </summary>
public class VirtualFileSet
{
    /// <summary>
    /// 文件过滤器
    /// </summary>
    public List<string> FilePatterns { get; }

    /// <summary>
    /// 根目录
    /// </summary>
    public string Root { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="root"></param>
    public VirtualFileSet(string root = "")
    {
        Root = root;
        FilePatterns = [];
    }

    /// <summary>
    /// 添加文件过滤器
    /// </summary>
    /// <param name="pattern">文件匹配模式</param>
    public void AddPattern(string pattern)
    {
        FilePatterns.Add(pattern);
    }

    /// <summary>
    /// 添加多个文件过滤器
    /// </summary>
    /// <param name="patterns">文件匹配模式列表</param>
    public void AddPatterns(params string[] patterns)
    {
        FilePatterns.AddRange(patterns);
    }
}
