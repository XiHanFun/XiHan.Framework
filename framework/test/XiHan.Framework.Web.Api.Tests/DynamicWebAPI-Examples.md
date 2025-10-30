# 动态 WebAPI 使用示例

本文档提供了动态 WebAPI 的完整使用示例，帮助您快速上手。

## 📝 目录

1. [基础 CRUD 示例](#基础-crud-示例)
2. [批量操作示例](#批量操作示例)
3. [高级查询示例](#高级查询示例)
4. [自定义方法示例](#自定义方法示例)
5. [版本控制示例](#版本控制示例)
6. [完整项目示例](#完整项目示例)

## 基础 CRUD 示例

### 1. 产品管理服务

```csharp
using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace EShop.Application.Services;

// 实体定义
public class Product : FullAuditedEntityBase<long>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
}

// DTO 定义
public class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset CreatedTime { get; set; }
}

public class CreateProductDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;
}

public class UpdateProductDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

// 应用服务
[DynamicApi]
public class ProductAppService : CrudApplicationServiceBase<Product, ProductDto, long, CreateProductDto, UpdateProductDto>
{
    public ProductAppService(IRepositoryBase<Product, long> repository) : base(repository)
    {
    }

    protected override async Task<ProductDto> MapToEntityDtoAsync(Product entity)
    {
        return new ProductDto
        {
            Id = entity.BasicId,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Stock = entity.Stock,
            Category = entity.Category,
            CreatedTime = entity.CreatedTime
        };
    }

    protected override async Task<Product> MapToEntityAsync(CreateProductDto createDto)
    {
        return new Product
        {
            Name = createDto.Name,
            Description = createDto.Description,
            Price = createDto.Price,
            Stock = createDto.Stock,
            Category = createDto.Category
        };
    }

    protected override async Task MapToEntityAsync(UpdateProductDto updateDto, Product entity)
    {
        entity.Description = updateDto.Description;
        entity.Price = updateDto.Price;
        entity.Stock = updateDto.Stock;
    }
}
```

### 自动生成的 API

```
GET    /api/products              # 获取产品列表
GET    /api/products/{id}         # 获取单个产品
POST   /api/products              # 创建产品
PUT    /api/products/{id}         # 更新产品
DELETE /api/products/{id}         # 删除产品
```

### API 调用示例

```javascript
// 获取产品列表
fetch("/api/products?pageIndex=1&pageSize=10")
  .then((response) => response.json())
  .then((data) => console.log(data));

// 创建产品
fetch("/api/products", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    name: "笔记本电脑",
    description: "高性能笔记本",
    price: 5999.0,
    stock: 100,
    category: "电子产品",
  }),
})
  .then((response) => response.json())
  .then((data) => console.log(data));
```

## 批量操作示例

### 2. 订单批量处理服务

```csharp
using XiHan.Framework.Application.Services;
using XiHan.Framework.Web.Api.DynamicApi.Batch;

namespace EShop.Application.Services;

// 订单实体
public class Order : FullAuditedEntityBase<long>
{
    public string OrderNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// 订单 DTO
public class OrderDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTimeOffset CreatedTime { get; set; }
}

public class OrderItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal => Quantity * UnitPrice;
}

public class CreateOrderDto
{
    [Required]
    public long CustomerId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    [Required]
    public long ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

// 批量操作服务
[DynamicApi]
public class OrderAppService : BatchCrudApplicationServiceBase<Order, OrderDto, long, CreateOrderDto, UpdateOrderDto>
{
    private readonly IRepositoryBase<Product, long> _productRepository;

    public OrderAppService(
        IRepositoryBase<Order, long> repository,
        IRepositoryBase<Product, long> productRepository) : base(repository)
    {
        _productRepository = productRepository;
    }

    protected override async Task<OrderDto> MapToEntityDtoAsync(Order entity)
    {
        return new OrderDto
        {
            Id = entity.BasicId,
            OrderNo = entity.OrderNo,
            CustomerId = entity.CustomerId,
            TotalAmount = entity.TotalAmount,
            Status = entity.Status,
            Items = entity.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            CreatedTime = entity.CreatedTime
        };
    }

    protected override async Task<Order> MapToEntityAsync(CreateOrderDto createDto)
    {
        var order = new Order
        {
            OrderNo = GenerateOrderNo(),
            CustomerId = createDto.CustomerId,
            Status = "Pending"
        };

        foreach (var item in createDto.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            if (product == null)
            {
                throw new BusinessException($"产品 {item.ProductId} 不存在");
            }

            order.Items.Add(new OrderItem
            {
                ProductId = product.BasicId,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        return order;
    }

    /// <summary>
    /// 批量确认订单
    /// </summary>
    [HttpPost("batch-confirm")]
    public async Task<BatchOperationResponse<OrderDto>> BatchConfirmAsync(List<long> orderIds)
    {
        var request = new BatchUpdateRequest<long, UpdateOrderDto>
        {
            Items = orderIds.Select(id => new BatchUpdateItem<long, UpdateOrderDto>
            {
                Id = id,
                Data = new UpdateOrderDto { Status = "Confirmed" }
            }).ToList(),
            UseTransaction = true,
            ContinueOnError = false
        };

        return await BatchUpdateAsync(request);
    }

    /// <summary>
    /// 批量取消订单
    /// </summary>
    [HttpPost("batch-cancel")]
    public async Task<BatchOperationResponse<OrderDto>> BatchCancelAsync(BatchDeleteRequest<long> request)
    {
        request.SoftDelete = true; // 使用软删除
        return await BatchDeleteAsync(request);
    }

    private string GenerateOrderNo()
    {
        return $"ORD{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}
```

### 批量操作 API 调用

```javascript
// 批量创建订单
fetch("/api/orders/batch-create", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    items: [
      {
        customerId: 1001,
        items: [
          { productId: 1, quantity: 2 },
          { productId: 2, quantity: 1 },
        ],
      },
      {
        customerId: 1002,
        items: [{ productId: 3, quantity: 5 }],
      },
    ],
    useTransaction: true,
    continueOnError: false,
  }),
})
  .then((response) => response.json())
  .then((data) => {
    console.log(`成功: ${data.successCount}, 失败: ${data.failureCount}`);
    console.log("结果:", data.results);
  });

// 批量确认订单
fetch("/api/orders/batch-confirm", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify([1001, 1002, 1003]),
})
  .then((response) => response.json())
  .then((data) => console.log(data));
```

## 高级查询示例

### 3. 高级搜索服务

```csharp
[DynamicApi]
public class ProductSearchAppService : ApplicationServiceBase
{
    private readonly IRepositoryBase<Product, long> _repository;

    public ProductSearchAppService(IRepositoryBase<Product, long> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 高级搜索
    /// </summary>
    [HttpPost("search")]
    public async Task<PageResponseDto<ProductDto>> SearchAsync(ProductSearchInput input)
    {
        var query = await _repository.GetQueryableAsync();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            query = query.Where(p =>
                p.Name.Contains(input.Keyword) ||
                p.Description.Contains(input.Keyword));
        }

        // 分类筛选
        if (!string.IsNullOrWhiteSpace(input.Category))
        {
            query = query.Where(p => p.Category == input.Category);
        }

        // 价格范围
        if (input.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= input.MinPrice.Value);
        }
        if (input.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= input.MaxPrice.Value);
        }

        // 库存筛选
        if (input.InStockOnly)
        {
            query = query.Where(p => p.Stock > 0);
        }

        // 排序
        query = input.SortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedTime)
        };

        // 分页
        var totalCount = await _repository.CountAsync(query);
        var items = await _repository.GetListAsync(
            query.Skip((input.PageIndex - 1) * input.PageSize)
                 .Take(input.PageSize));

        var dtos = items.Select(MapToDto).ToList();

        return new PageResponseDto<ProductDto>(
            new PageData
            {
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize)
            },
            dtos);
    }

    /// <summary>
    /// 获取热门产品
    /// </summary>
    [HttpGet("popular")]
    public async Task<List<ProductDto>> GetPopularProductsAsync([FromQuery] int top = 10)
    {
        var products = await _repository.GetListAsync(
            q => q.Where(p => p.Stock > 0)
                  .OrderByDescending(p => p.ViewCount)
                  .Take(top));

        return products.Select(MapToDto).ToList();
    }

    private ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.BasicId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            Category = product.Category,
            CreatedTime = product.CreatedTime
        };
    }
}

// 搜索输入 DTO
public class ProductSearchInput
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public string SortBy { get; set; } = "default";
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

## 自定义方法示例

### 4. 用户管理服务

```csharp
[DynamicApi]
[Authorize]
public class UserManagementAppService : CrudApplicationServiceBase<User, UserDto, long>
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public UserManagementAppService(
        IRepositoryBase<User, long> repository,
        IPasswordHasher passwordHasher,
        IEmailService emailService) : base(repository)
    {
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    /// <summary>
    /// 更改密码
    /// </summary>
    [HttpPost("{id}/change-password")]
    public async Task<bool> ChangePasswordAsync(long id, ChangePasswordInput input)
    {
        var user = await Repository.GetAsync(id);
        if (user == null)
        {
            throw new BusinessException("用户不存在");
        }

        if (!_passwordHasher.Verify(input.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessException("当前密码不正确");
        }

        user.PasswordHash = _passwordHasher.Hash(input.NewPassword);
        await Repository.UpdateAsync(user);

        return true;
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<string> ResetPasswordAsync(long id)
    {
        var user = await Repository.GetAsync(id);
        if (user == null)
        {
            throw new BusinessException("用户不存在");
        }

        var newPassword = GenerateRandomPassword();
        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await Repository.UpdateAsync(user);

        await _emailService.SendPasswordResetEmailAsync(user.Email, newPassword);

        return "密码已重置并发送至用户邮箱";
    }

    /// <summary>
    /// 锁定用户
    /// </summary>
    [HttpPost("{id}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<bool> LockUserAsync(long id, [FromQuery] int durationMinutes = 30)
    {
        var user = await Repository.GetAsync(id);
        if (user == null) return false;

        user.IsLocked = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(durationMinutes);
        await Repository.UpdateAsync(user);

        return true;
    }

    /// <summary>
    /// 解锁用户
    /// </summary>
    [HttpPost("{id}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<bool> UnlockUserAsync(long id)
    {
        var user = await Repository.GetAsync(id);
        if (user == null) return false;

        user.IsLocked = false;
        user.LockoutEnd = null;
        await Repository.UpdateAsync(user);

        return true;
    }

    /// <summary>
    /// 获取用户统计信息
    /// </summary>
    [HttpGet("{id}/statistics")]
    public async Task<UserStatisticsDto> GetStatisticsAsync(long id)
    {
        var user = await Repository.GetAsync(id);
        if (user == null)
        {
            throw new BusinessException("用户不存在");
        }

        return new UserStatisticsDto
        {
            UserId = id,
            TotalOrders = user.Orders.Count,
            TotalSpent = user.Orders.Sum(o => o.TotalAmount),
            LastLoginTime = user.LastLoginTime,
            RegistrationDays = (int)(DateTimeOffset.UtcNow - user.CreatedTime).TotalDays
        };
    }

    private string GenerateRandomPassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
```

## 版本控制示例

### 5. API 版本演进

```csharp
// V1 版本
[DynamicApi]
[ApiVersion("1.0")]
public class ProductV1AppService : CrudApplicationServiceBase<Product, ProductDtoV1, long>
{
    public ProductV1AppService(IRepositoryBase<Product, long> repository) : base(repository)
    {
    }

    // V1 的实现...
}

// V2 版本 - 添加了新字段和功能
[DynamicApi]
[ApiVersion("2.0")]
public class ProductV2AppService : CrudApplicationServiceBase<Product, ProductDtoV2, long>
{
    public ProductV2AppService(IRepositoryBase<Product, long> repository) : base(repository)
    {
    }

    /// <summary>
    /// V2 新增：获取产品评价
    /// </summary>
    [HttpGet("{id}/reviews")]
    public async Task<List<ProductReviewDto>> GetReviewsAsync(long id)
    {
        // V2 新功能实现
    }
}

// V1 DTO
public class ProductDtoV1
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// V2 DTO - 扩展了更多字段
public class ProductDtoV2
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public double AverageRating { get; set; } // V2 新增
    public int ReviewCount { get; set; }      // V2 新增
    public List<string> Images { get; set; } = new(); // V2 新增
}
```

### API 版本访问

```javascript
// 访问 V1 API
fetch("/api/v1/products/1")
  .then((response) => response.json())
  .then((data) => console.log(data));

// 访问 V2 API
fetch("/api/v2/products/1")
  .then((response) => response.json())
  .then((data) => console.log(data));

// V2 特有的功能
fetch("/api/v2/products/1/reviews")
  .then((response) => response.json())
  .then((data) => console.log(data));
```

## 完整项目示例

完整的电商系统示例代码请参考：

- GitHub: [XiHan.Framework.Samples.EShop](https://github.com/XiHanFun/XiHan.Framework.Samples.EShop)
- Gitee: [XiHan.Framework.Samples.EShop](https://gitee.com/XiHanFun/XiHan.Framework.Samples.EShop)

该示例项目包含：

- ✅ 完整的产品管理
- ✅ 订单处理流程
- ✅ 用户权限管理
- ✅ 购物车功能
- ✅ 批量操作演示
- ✅ API 版本控制
- ✅ 完整的单元测试
- ✅ Swagger 文档

## 总结

通过以上示例，您可以看到动态 WebAPI 功能的强大和灵活性。它能够：

1. **快速开发** - 无需编写控制器，专注于业务逻辑
2. **标准化** - 统一的 API 设计和命名规范
3. **可扩展** - 支持自定义方法和复杂业务场景
4. **易维护** - 清晰的代码结构和分层设计

开始使用动态 WebAPI，让您的 API 开发更加高效！
