#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateCompiler
// Guid:5c0f4e9a-3d6f-4b9c-0f5e-4a9d6f3c0f5e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Parsers;

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板编译器接口
/// </summary>
/// <typeparam name="T">编译结果类型</typeparam>
public interface ITemplateCompiler<T>
{
    /// <summary>
    /// 编译模板
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">编译选项</param>
    /// <returns>编译结果</returns>
    T Compile(ITemplateAst ast, TemplateCompilerOptions? options = null);

    /// <summary>
    /// 编译模板源码
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">编译选项</param>
    /// <returns>编译结果</returns>
    T Compile(string templateSource, TemplateCompilerOptions? options = null);

    /// <summary>
    /// 异步编译模板
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">编译选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编译结果</returns>
    Task<T> CompileAsync(ITemplateAst ast, TemplateCompilerOptions? options = null, CancellationToken cancellationToken = default);
}
