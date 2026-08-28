// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 属性或字段自动装配服务处理器
/// </summary>
/// <remarks>由此启发：<see href="https://www.cnblogs.com/loogn/p/10566510.html"/></remarks>
public class AutowiredServiceHandler
{
    private readonly IServiceProvider _serviceProvider;

    // 该处理器在 InternalServiceCollectionExtensions 里以单例注册，同一实例会被多线程并发调用。
    // 原先是普通 Dictionary，未命中缓存时直接写入且无任何同步，并发写会破坏字典内部结构
    // （轻则丢失缓存项，重则读取时死循环）。改用 ConcurrentDictionary：
    // 并发下最多重复编译一次功能等价的赋值委托，属可接受的浪费，但缓存结构不会被写坏。
    private readonly ConcurrentDictionary<Type, Action<object, IServiceProvider>> _autowiredActions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public AutowiredServiceHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 装配属性和字段
    /// </summary>
    /// <param name="service"></param>
    public void Autowired(object service)
    {
        Autowired(service, _serviceProvider);
    }

    /// <summary>
    /// 装配属性和字段
    /// </summary>
    /// <param name="service"></param>
    /// <param name="serviceProvider"></param>
    private void Autowired(object service, IServiceProvider serviceProvider)
    {
        var serviceType = service.GetType();
        if (_autowiredActions.TryGetValue(serviceType, out var act))
        {
            act(service, serviceProvider);
        }
        else
        {
            //参数
            var objParam = Expression.Parameter(typeof(object), "obj");
            var spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
            var obj = Expression.Convert(objParam, serviceType);
            var getService = typeof(IServiceProvider).GetMethod("GetService");

            List<Expression> setList = [];
            if (getService is not null)
            {
                // 字段赋值
                setList.AddRange(
                    from field in serviceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let autowiredAttr = field.GetCustomAttribute<AutowiredServiceAttribute>()
                    where autowiredAttr is not null
                    let fieldExp = Expression.Field(obj, field)
                    let createService = Expression.Call(spParam, getService, Expression.Constant(field.FieldType))
                    select Expression.Assign(fieldExp, Expression.Convert(createService, field.FieldType)));
                // 属性赋值
                setList.AddRange(
                    from property in serviceType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let autowiredAttr = property.GetCustomAttribute<AutowiredServiceAttribute>()
                    where autowiredAttr is not null
                    let propExp = Expression.Property(obj, property)
                    let createService = Expression.Call(spParam, getService, Expression.Constant(property.PropertyType))
                    select Expression.Assign(propExp, Expression.Convert(createService, property.PropertyType)));
            }

            var bodyExp = Expression.Block(setList);
            var setAction = Expression.Lambda<Action<object, IServiceProvider>>(bodyExp, objParam, spParam).Compile();
            _autowiredActions[serviceType] = setAction;
            setAction(service, serviceProvider);
        }
    }
}
