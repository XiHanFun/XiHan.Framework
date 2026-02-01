#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateContextBuilder
// Guid:ec387966-d24f-4893-bc7c-bd8f600400a4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:06:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文构建器
/// </summary>
public class TemplateContextBuilder : ITemplateContextBuilder
{
    private readonly ITemplateContextFactory _factory;
    private readonly Dictionary<string, object?> _variables = [];
    private readonly Dictionary<string, Delegate> _functions = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="factory">上下文工厂</param>
    public TemplateContextBuilder(ITemplateContextFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 添加变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    public ITemplateContextBuilder AddVariable(string name, object? value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// 添加变量字典
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <returns>构建器实例</returns>
    public ITemplateContextBuilder AddVariables(IDictionary<string, object?> variables)
    {
        foreach (var (key, value) in variables)
        {
            _variables[key] = value;
        }
        return this;
    }

    /// <summary>
    /// 添加对象属性
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="prefix">属性前缀</param>
    /// <returns>构建器实例</returns>
    public ITemplateContextBuilder AddObject(object obj, string? prefix = null)
    {
        if (obj == null)
        {
            return this;
        }

        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                var name = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                var value = property.GetValue(obj);
                _variables[name] = value;
            }
        }

        return this;
    }

    /// <summary>
    /// 添加函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="function">函数委托</param>
    /// <returns>构建器实例</returns>
    public ITemplateContextBuilder AddFunction(string name, Delegate function)
    {
        _functions[name] = function;
        return this;
    }

    /// <summary>
    /// 添加全局函数
    /// </summary>
    /// <param name="type">包含静态方法的类型</param>
    /// <returns>构建器实例</returns>
    public ITemplateContextBuilder AddGlobalFunctions(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (method.IsPublic && method.IsStatic && !method.IsSpecialName)
            {
                var name = method.Name;
                var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                var returnType = method.ReturnType;

                // 创建委托类型
                Type? delegateType;
                if (returnType == typeof(void))
                {
                    delegateType = parameterTypes.Length switch
                    {
                        0 => typeof(Action),
                        1 => typeof(Action<>).MakeGenericType(parameterTypes),
                        2 => typeof(Action<,>).MakeGenericType(parameterTypes),
                        3 => typeof(Action<,,>).MakeGenericType(parameterTypes),
                        4 => typeof(Action<,,,>).MakeGenericType(parameterTypes),
                        _ => null // 不支持超过4个参数的Action
                    };
                }
                else
                {
                    var allTypes = parameterTypes.Concat([returnType]).ToArray();
                    delegateType = allTypes.Length switch
                    {
                        1 => typeof(Func<>).MakeGenericType(allTypes),
                        2 => typeof(Func<,>).MakeGenericType(allTypes),
                        3 => typeof(Func<,,>).MakeGenericType(allTypes),
                        4 => typeof(Func<,,,>).MakeGenericType(allTypes),
                        5 => typeof(Func<,,,,>).MakeGenericType(allTypes),
                        _ => null // 不支持超过4个参数的Func
                    };
                }

                if (delegateType != null)
                {
                    var methodDelegate = Delegate.CreateDelegate(delegateType, method);
                    _functions[name] = methodDelegate;
                }
            }
        }

        return this;
    }

    /// <summary>
    /// 构建上下文
    /// </summary>
    /// <returns>模板上下文</returns>
    public ITemplateContext Build()
    {
        var context = _factory.CreateContext(_variables);

        foreach (var (name, function) in _functions)
        {
            context.SetFunction(name, function);
        }

        return context;
    }
}
