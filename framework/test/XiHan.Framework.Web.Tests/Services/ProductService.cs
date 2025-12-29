#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProductService
// Guid:product-service-impl-test-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Web.Tests.Services.Dtos;

namespace XiHan.Framework.Web.Tests.Services;

/// <summary>
/// 产品服务实现
/// </summary>
public class ProductService : IProductService
{
    // 模拟内存数据库
    private static readonly List<ProductDto> Products = new()
    {
        new ProductDto { Id = 1, Name = "笔记本电脑", Description = "高性能办公笔记本", Price = 5999m, Stock = 50, IsActive = true, CreatedTime = DateTime.Now.AddDays(-30) },
        new ProductDto { Id = 2, Name = "无线鼠标", Description = "人体工学设计", Price = 99m, Stock = 200, IsActive = true, CreatedTime = DateTime.Now.AddDays(-20) },
        new ProductDto { Id = 3, Name = "机械键盘", Description = "青轴机械键盘", Price = 399m, Stock = 100, IsActive = true, CreatedTime = DateTime.Now.AddDays(-15) },
        new ProductDto { Id = 4, Name = "显示器", Description = "27寸 4K 显示器", Price = 2499m, Stock = 30, IsActive = false, CreatedTime = DateTime.Now.AddDays(-10) }
    };

    private static int _nextId = 5;

    /// <summary>
    /// 获取产品
    /// </summary>
    public Task<ProductDto?> GetAsync(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(product);
    }

    /// <summary>
    /// 获取所有产品
    /// </summary>
    public Task<List<ProductDto>> GetListAsync()
    {
        return Task.FromResult(Products.Where(p => p.IsActive).ToList());
    }

    /// <summary>
    /// 搜索产品
    /// </summary>
    public Task<List<ProductDto>> SearchAsync(string keyword)
    {
        var result = Products.Where(p =>
            p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            (p.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// 创建产品
    /// </summary>
    public Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        var product = new ProductDto
        {
            Id = _nextId++,
            Name = input.Name,
            Description = input.Description,
            Price = input.Price,
            Stock = input.Stock,
            IsActive = true,
            CreatedTime = DateTime.Now
        };

        Products.Add(product);
        return Task.FromResult(product);
    }

    /// <summary>
    /// 更新产品
    /// </summary>
    public Task<ProductDto> UpdateAsync(int id, UpdateProductDto input)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            throw new Exception($"产品 {id} 不存在");
        }

        if (!string.IsNullOrEmpty(input.Name))
        {
            product.Name = input.Name;
        }

        if (input.Description != null)
        {
            product.Description = input.Description;
        }

        if (input.Price.HasValue)
        {
            product.Price = input.Price.Value;
        }

        if (input.Stock.HasValue)
        {
            product.Stock = input.Stock.Value;
        }

        return Task.FromResult(product);
    }

    /// <summary>
    /// 删除产品
    /// </summary>
    public Task DeleteAsync(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product != null)
        {
            Products.Remove(product);
        }

        return Task.CompletedTask;
    }
}
