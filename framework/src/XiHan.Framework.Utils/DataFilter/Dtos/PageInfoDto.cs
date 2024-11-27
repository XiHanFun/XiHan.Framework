#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageInfoDto
// Guid:96c7c379-cafa-4826-a6ba-9dc0b6c33ca8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:45:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DataFilter.Dtos;

/// <summary>
/// 通用分页信息基类
/// </summary>
public class PageInfoDto
{
    #region 默认值

    /// <summary>
    /// 默认当前页(防止非安全性传参)
    /// </summary>
    private const int DefaultIndex = 1;

    /// <summary>
    /// 默认每页大小最大值(防止非安全性传参)
    /// </summary>
    private const int DefaultMaxPageSize = 100;

    /// <summary>
    /// 默认每页大小最小值(防止非安全性传参)
    /// </summary>
    private const int DefaultMinPageSize = 1;

    #endregion 默认值

    private readonly int _currentIndex = 1;
    private readonly int _pageSize = 20;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PageInfoDto()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="pageSize"></param>
    public PageInfoDto(int currentIndex, int pageSize)
    {
        CurrentIndex = currentIndex;
        PageSize = pageSize;
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
            value = value switch
            {
                > DefaultMaxPageSize => DefaultMaxPageSize,
                < DefaultMinPageSize => DefaultMinPageSize,
                _ => value
            };

            _pageSize = value;
        }
    }
}
