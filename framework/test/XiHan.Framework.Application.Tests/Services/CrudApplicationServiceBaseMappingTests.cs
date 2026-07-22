// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Entities;
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
        var entity = new TestEntity(1) { Name = "old-name", Age = 18 };
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
        var entity = new TestEntity(1) { Name = "old-name", Age = 18 };
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

    private sealed class TestEntity : EntityBase<long>
    {
        public TestEntity()
        {
        }

        public TestEntity(long basicId)
            : base(basicId)
        {
        }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestEntityDto : DtoBase<long>
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestCreateDto : CreationDtoBase<long>
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestUpdateDto : UpdateDtoBase<long>
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class TestPageRequestDto : PageRequestDtoBase
    {
    }
}
