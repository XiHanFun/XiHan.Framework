// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// XML 文档成员名取样服务
/// </summary>
/// <remarks>
/// 这些签名不是随手写的：每个方法各自命中 XML 文档 id 规则的一条分支
/// （无参、基元、可空值、ref/out/in、锯齿数组、构造泛型、嵌套类型、泛型方法），
/// 期望值按 Roslyn 生成的文档注释 id 书写。方法不允许重载，用例用 GetMethod(name) 单值取。
/// </remarks>
public class XmlDocSampleService
{
    /// <summary>
    /// 无参方法
    /// </summary>
    public void NoParameters()
    {
    }

    /// <summary>
    /// 基元参数方法
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="count">数量</param>
    public void Primitives(string name, int count)
    {
    }

    /// <summary>
    /// 可空值参数方法
    /// </summary>
    /// <param name="id">标识</param>
    public void NullableValue(int? id)
    {
    }

    /// <summary>
    /// 引用传递参数方法
    /// </summary>
    /// <param name="counter">计数器</param>
    /// <param name="message">消息</param>
    public void ByRefParameters(ref int counter, out string message)
    {
        counter++;
        message = string.Empty;
    }

    /// <summary>
    /// 只读引用传递参数方法
    /// </summary>
    /// <param name="amount">金额</param>
    public void InParameter(in decimal amount)
    {
    }

    /// <summary>
    /// 数组参数方法
    /// </summary>
    /// <param name="names">名称数组</param>
    /// <param name="matrix">锯齿数组</param>
    public void ArrayParameters(string[] names, int[][] matrix)
    {
    }

    /// <summary>
    /// 构造泛型参数方法
    /// </summary>
    /// <param name="names">名称集合</param>
    /// <param name="map">映射</param>
    public void GenericContainers(List<string> names, Dictionary<string, int> map)
    {
    }

    /// <summary>
    /// 嵌套类型参数方法
    /// </summary>
    /// <param name="payload">载荷</param>
    public void NestedTypeParameter(NestedPayload payload)
    {
    }

    /// <summary>
    /// 泛型方法
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="values">值集合</param>
    public void GenericMethod<TValue>(TValue value, IReadOnlyList<TValue> values)
    {
    }

    /// <summary>
    /// 嵌套载荷类型
    /// </summary>
    public class NestedPayload
    {
    }
}

/// <summary>
/// 泛型声明类型取样仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class XmlDocSampleRepository<TEntity>
{
    /// <summary>
    /// 保存实体
    /// </summary>
    /// <param name="entity">实体</param>
    public void Save(TEntity entity)
    {
    }

    /// <summary>
    /// 探活
    /// </summary>
    public void Ping()
    {
    }
}

/// <summary>
/// 虚方法取样基类
/// </summary>
public class XmlDocSampleBase
{
    /// <summary>
    /// 执行
    /// </summary>
    /// <param name="input">输入</param>
    public virtual void Run(string input)
    {
    }
}

/// <summary>
/// 重写虚方法的取样派生类
/// </summary>
public class XmlDocSampleDerived : XmlDocSampleBase
{
    /// <summary>
    /// 执行
    /// </summary>
    /// <param name="input">输入</param>
    public override void Run(string input)
    {
    }
}

/// <summary>
/// 泛型取样基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class XmlDocSampleGenericBase<TEntity>
{
    /// <summary>
    /// 处理实体
    /// </summary>
    /// <param name="entity">实体</param>
    public virtual void Handle(TEntity entity)
    {
    }
}

/// <summary>
/// 闭合泛型基类的取样实现
/// </summary>
public class XmlDocSampleStringHandler : XmlDocSampleGenericBase<string>
{
    /// <summary>
    /// 处理实体
    /// </summary>
    /// <param name="entity">实体</param>
    public override void Handle(string entity)
    {
    }
}
