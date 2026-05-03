using System.Collections;
using System.Reflection;

namespace XiHan.Framework.Kernel.Collections;

/// <summary>
/// 提供以 <see cref="object"/> 为基础类型的类型列表快捷实现。
/// </summary>
public class TypeList : TypeList<object>, ITypeList;

/// <summary>
/// 提供带基础类型约束的类型列表实现。
/// </summary>
/// <typeparam name="TBaseType">基础类型。</typeparam>
public class TypeList<TBaseType> : ITypeList<TBaseType>
{
    private readonly List<Type> _typeList = [];

    /// <inheritdoc />
    public int Count => _typeList.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public Type this[int index]
    {
        get => _typeList[index];
        set
        {
            CheckType(value);
            _typeList[index] = value;
        }
    }

    /// <inheritdoc />
    public void Add<T>() where T : TBaseType
    {
        _typeList.Add(typeof(T));
    }

    /// <inheritdoc />
    public bool TryAdd<T>() where T : TBaseType
    {
        if (Contains<T>())
        {
            return false;
        }

        Add<T>();
        return true;
    }

    /// <inheritdoc />
    public void Add(Type item)
    {
        CheckType(item);
        _typeList.Add(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _typeList.Clear();
    }

    /// <inheritdoc />
    public bool Contains<T>() where T : TBaseType
    {
        return Contains(typeof(T));
    }

    /// <inheritdoc />
    public bool Contains(Type item)
    {
        return _typeList.Contains(item);
    }

    /// <inheritdoc />
    public void CopyTo(Type[] array, int arrayIndex)
    {
        _typeList.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator()
    {
        return _typeList.GetEnumerator();
    }

    /// <inheritdoc />
    public int IndexOf(Type item)
    {
        return _typeList.IndexOf(item);
    }

    /// <inheritdoc />
    public void Insert(int index, Type item)
    {
        CheckType(item);
        _typeList.Insert(index, item);
    }

    /// <inheritdoc />
    public void Remove<T>() where T : TBaseType
    {
        _typeList.Remove(typeof(T));
    }

    /// <inheritdoc />
    public bool Remove(Type item)
    {
        return _typeList.Remove(item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        _typeList.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _typeList.GetEnumerator();
    }

    private static void CheckType(Type item)
    {
        if (!typeof(TBaseType).GetTypeInfo().IsAssignableFrom(item))
        {
            throw new ArgumentException($"给定类型 ({item.AssemblyQualifiedName}) 必须可赋值给 {typeof(TBaseType).AssemblyQualifiedName}。", nameof(item));
        }
    }
}
