#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageInfoDto
// Guid:96c7c379-cafa-4826-a6ba-9dc0b6c33ca8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:45:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Paging.Dtos;

/// <summary>
/// 通用分页信息基类
/// </summary>
public class PageInfo
{
    #region 默认值

    /// <summary>
    /// 默认当前页(防止非安全性传参)
    /// </summary>
    private const int DefaultIndex = 1;

    /// <summary>
    /// 默认每页大小(防止非安全性传参)
    /// </summary>
    private const int DefaultPageSize = 20;

    #endregion 默认值

    private readonly int _currentIndex = DefaultIndex;
    private readonly int _pageSize = DefaultPageSize;
    private readonly int[] _defaultPageSizeArray = [10, 20, 50, 100, 200, 500];

    /// <summary>
    /// 构造函数
    /// </summary>
    public PageInfo()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="pageSize"></param>
    public PageInfo(int currentIndex, int pageSize)
    {
        _currentIndex = currentIndex;
        _pageSize = pageSize;
    }

    /// <summary>
    /// 当前页标
    /// </summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        init
        {
            if (value < DefaultIndex)
            {
                value = DefaultIndex;
            }

            _currentIndex = value;
        }
    }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init
        {
            var defaultMaxPageSize = _defaultPageSizeArray.Max();
            var defaultMinPageSize = _defaultPageSizeArray.Min();

            value = value switch
            {
                _ when value > defaultMaxPageSize => defaultMaxPageSize,
                _ when value < defaultMinPageSize => defaultMinPageSize,
                // 不在默认每页大小数组中的值，取最接近的默认值
                _ => _defaultPageSizeArray.OrderBy(p => Math.Abs(p - value)).First()
            };

            _pageSize = value;
        }
    }
}
