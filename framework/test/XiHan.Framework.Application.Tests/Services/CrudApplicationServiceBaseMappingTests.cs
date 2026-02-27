#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CrudApplicationServiceBaseMappingTests
// Guid:8b39870b-0aca-45be-a21d-2e012e95073c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 03:36:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Tests.Services;

/// <summary>
/// CrudApplicationServiceBase 映射测试
/// </summary>
public class CrudApplicationServiceBaseMappingTests
{
    [Fact]
    public async Task MapUpdateDtoToEntity_ShouldMutateExistingInstance()
    {
        var service = new TestCrudService();
        var entity = new TestEntity { BasicId = 1, Name = "old-name", Age = 18 };
        var updateDto = new TestUpdateDto { Name = "new-name", Age = 20 };

        var beforeRef = entity;
        await service.ApplyUpdateDtoAsync(updateDto, entity);

        Assert.Same(beforeRef, entity);
        Assert.Equal("new-name", entity.Name);
        Assert.Equal(20, entity.Age);
    }

    [Fact]
    public async Task MapEntityDtoToEntity_ShouldMutateExistingInstance()
    {
        var service = new TestCrudService();
        var entity = new TestEntity { BasicId = 1, Name = "old-name", Age = 18 };
        var dto = new TestEntityDto { Name = "dto-name", Age = 25 };

        var beforeRef = entity;
        await service.ApplyEntityDtoAsync(dto, entity);

        Assert.Same(beforeRef, entity);
        Assert.Equal("dto-name", entity.Name);
        Assert.Equal(25, entity.Age);
    }

    private sealed class TestCrudService
        : CrudApplicationServiceBase<TestEntity, TestEntityDto, long, TestCreateDto, TestUpdateDto, TestPageRequestDto>
    {
        public TestCrudService()
            : base(null!)
        {
        }

        public Task ApplyUpdateDtoAsync(TestUpdateDto dto, TestEntity entity)
        {
            return MapDtoToEntityAsync(dto, entity);
        }

        public Task ApplyEntityDtoAsync(TestEntityDto dto, TestEntity entity)
        {
            return MapDtoToEntityAsync(dto, entity);
        }
    }

    private sealed class TestEntity : IEntityBase<long>
    {
        public long BasicId { get; set; }

        public long RowVersion { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public bool IsTransient()
        {
            return BasicId <= 0;
        }
    }

    private sealed class TestEntityDto
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestCreateDto
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestUpdateDto
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestPageRequestDto : PageRequestDtoBase
    {
    }
}
