#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IProductService
// Guid:product-service-interface-test-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Services;
using XiHan.Framework.Web.Tests.Services.Dtos;

namespace XiHan.Framework.Web.Tests.Services;

/// <summary>
/// 产品服务接口
/// </summary>
public interface IProductService : IApplicationService
{
    /// <summary>
    /// 获取产品
    /// </summary>
    /// <param name="id">产品 ID</param>
    Task<ProductDto?> GetAsync(int id);

    /// <summary>
    /// 获取所有产品
    /// </summary>
    Task<List<ProductDto>> GetListAsync();

    /// <summary>
    /// 搜索产品
    /// </summary>
    /// <param name="keyword">关键词</param>
    Task<List<ProductDto>> SearchAsync(string keyword);

    /// <summary>
    /// 创建产品
    /// </summary>
    Task<ProductDto> CreateAsync(CreateProductDto input);

    /// <summary>
    /// 更新产品
    /// </summary>
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto input);

    /// <summary>
    /// 删除产品
    /// </summary>
    Task DeleteAsync(int id);
}
