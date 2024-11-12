#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeList
// Guid:6c41a075-9f6e-4bcc-ae17-8fd2ac1c991e
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-24 下午 03:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Reflection;

namespace XiHan.Framework.Core.Collections;

/// <summary>
/// 一个用于 <see cref="TypeList{TBaseType}"/> 的快捷方式，以使用对象作为基本类型
/// </summary>
public class TypeList : TypeList<object>, ITypeList
{
}

/// <summary>
/// 扩展 <see cref="List{Type}"/> 以添加对特定基本类型的限制
/// </summary>
/// <typeparam name="TBaseType"></typeparam>
public class TypeList<TBaseType> : ITypeList<TBaseType>
{
    /// <summary>
    /// 数量
    /// </summary>
    /// <value>数量</value>
    public int Count => _typeList.Count;

    /// <summary>
    /// 获取一个值，该值指示此实例是否为只读
    /// </summary>
    /// <value><c>如果此实例为只读，则为 true</c>；否则，<c>false</c>.</value>
    public bool IsReadOnly => false;

    /// <summary>
    /// 获取或设置指定索引处的 <see cref="Type"/>
    /// </summary>
    /// <param name="index">索引</param>
    public Type this[int index]
    {
        get => _typeList[index];
        set
        {
            CheckType(value);
            _typeList[index] = value;
        }
    }

    private readonly List<Type> _typeList;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TypeList()
    {
        _typeList = [];
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void Add<T>() where T : TBaseType
    {
        _typeList.Add(typeof(T));
    }

    /// <summary>
    /// 尝试添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool TryAdd<T>() where T : TBaseType
    {
        if (Contains<T>())
        {
            return false;
        }

        Add<T>();
        return true;
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="item"></param>
    public void Add(Type item)
    {
        CheckType(item);
        _typeList.Add(item);
    }

    /// <summary>
    /// 插入
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    public void Insert(int index, Type item)
    {
        CheckType(item);
        _typeList.Insert(index, item);
    }

    /// <summary>
    /// 项目索引
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public int IndexOf(Type item)
    {
        return _typeList.IndexOf(item);
    }

    /// <summary>
    /// 包含
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool Contains<T>() where T : TBaseType
    {
        return Contains(typeof(T));
    }

    /// <summary>
    /// 包含
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(Type item)
    {
        return _typeList.Contains(item);
    }

    /// <summary>
    /// 移除
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void Remove<T>() where T : TBaseType
    {
        _ = _typeList.Remove(typeof(T));
    }

    /// <summary>
    /// 按项目移除
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Remove(Type item)
    {
        return _typeList.Remove(item);
    }

    /// <summary>
    /// 按索引移除
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAt(int index)
    {
        _typeList.RemoveAt(index);
    }

    /// <summary>
    /// 清空
    /// </summary>
    public void Clear()
    {
        _typeList.Clear();
    }

    /// <summary>
    /// 拷贝
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    public void CopyTo(Type[] array, int arrayIndex)
    {
        _typeList.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    public IEnumerator<Type> GetEnumerator()
    {
        return _typeList.GetEnumerator();
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _typeList.GetEnumerator();
    }

    /// <summary>
    /// 校验类型
    /// </summary>
    /// <param name="item"></param>
    /// <exception cref="ArgumentException"></exception>
    private static void CheckType(Type item)
    {
        if (!typeof(TBaseType).GetTypeInfo().IsAssignableFrom(item))
        {
            throw new ArgumentException($"给定类型 ({item.AssemblyQualifiedName}) 应为 {typeof(TBaseType).AssemblyQualifiedName} 的实例。", nameof(item));
        }
    }
}
