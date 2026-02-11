#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateContext
// Guid:7f19cc9c-cedc-43fd-b4d7-cb291bcc233c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文接口
/// </summary>
public interface ITemplateContext
{
    /// <summary>
    /// 获取变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>变量值</returns>
    object? GetVariable(string name);

    /// <summary>
    /// 设置变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    void SetVariable(string name, object? value);

    /// <summary>
    /// 是否包含变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否包含</returns>
    bool HasVariable(string name);

    /// <summary>
    /// 移除变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否成功移除</returns>
    bool RemoveVariable(string name);

    /// <summary>
    /// 获取所有变量名
    /// </summary>
    /// <returns>变量名集合</returns>
    IEnumerable<string> GetVariableNames();

    /// <summary>
    /// 推入作用域
    /// </summary>
    /// <returns>作用域标识</returns>
    IDisposable PushScope();

    /// <summary>
    /// 获取函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <returns>函数委托</returns>
    Delegate? GetFunction(string name);

    /// <summary>
    /// 设置函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="function">函数委托</param>
    void SetFunction(string name, Delegate function);

    /// <summary>
    /// 克隆上下文
    /// </summary>
    /// <returns>新的上下文实例</returns>
    ITemplateContext Clone();
}
