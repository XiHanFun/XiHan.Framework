// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
