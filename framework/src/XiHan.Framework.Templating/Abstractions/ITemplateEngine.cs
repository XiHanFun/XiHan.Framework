#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateEngine
// Guid:3a8f2c9e-1b4d-4e7a-8f3c-2d9e1b4a7f3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板引擎接口
/// </summary>
/// <typeparam name="T">模板类型</typeparam>
public interface ITemplateEngine<T>
{
    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderAsync(T template, ITemplateContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 渲染模板（同步）
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    string Render(T template, ITemplateContext context);

    /// <summary>
    /// 解析模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析后的模板</returns>
    T Parse(string templateSource);

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult Validate(string templateSource);
}

/// <summary>
/// 模板引擎注册表
/// </summary>
public interface ITemplateEngineRegistry
{
    /// <summary>
    /// 注册模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <param name="engine">模板引擎实例</param>
    void RegisterEngine<T>(string engineName, ITemplateEngine<T> engine);

    /// <summary>
    /// 获取模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>模板引擎实例</returns>
    ITemplateEngine<T>? GetEngine<T>(string engineName);

    /// <summary>
    /// 获取默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <returns>默认模板引擎实例</returns>
    ITemplateEngine<T>? GetDefaultEngine<T>();

    /// <summary>
    /// 设置默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    void SetDefaultEngine<T>(string engineName);
}

/// <summary>
/// 模板验证结果
/// </summary>
public record TemplateValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 错误位置（行号）
    /// </summary>
    public int? ErrorLine { get; init; }

    /// <summary>
    /// 错误位置（列号）
    /// </summary>
    public int? ErrorColumn { get; init; }

    /// <summary>
    /// 成功验证结果
    /// </summary>
    public static TemplateValidationResult Success => new() { IsValid = true };

    /// <summary>
    /// 创建失败验证结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="line">错误行号</param>
    /// <param name="column">错误列号</param>
    /// <returns>验证结果</returns>
    public static TemplateValidationResult Failure(string errorMessage, int? line = null, int? column = null)
        => new() { IsValid = false, ErrorMessage = errorMessage, ErrorLine = line, ErrorColumn = column };
}
