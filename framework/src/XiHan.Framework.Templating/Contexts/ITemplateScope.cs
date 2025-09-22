#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateScope
// Guid:e225e597-de04-408f-af12-b3ee2f3eb812
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:53:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板作用域
/// </summary>
public interface ITemplateScope : IDisposable
{
    /// <summary>
    /// 作用域标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 父作用域
    /// </summary>
    ITemplateScope? Parent { get; }

    /// <summary>
    /// 是否为根作用域
    /// </summary>
    bool IsRoot { get; }

    /// <summary>
    /// 作用域变量
    /// </summary>
    IDictionary<string, object?> Variables { get; }
}
