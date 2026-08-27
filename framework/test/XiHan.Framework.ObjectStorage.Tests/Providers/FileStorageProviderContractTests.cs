// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;

namespace XiHan.Framework.ObjectStorage.Tests.Providers;

/// <summary>
/// 内置文件存储提供程序的构造契约测试
/// </summary>
/// <remarks>
/// 三个云端 Provider 的构造函数会直接创建 SDK 客户端（OssClient / MinioClient / CosXmlServer），
/// 实例化本身就是一次带凭据的初始化，不适合在单元测试里做，因此这里只用反射守住
/// 「继承自基类 + 唯一的 IOptions&lt;TOptions&gt; 构造函数」这条 DI 能否装配起来的硬契约。
/// 它们的读写行为必须依赖真实云端，属于集成测试范畴，这里有意不覆盖。
/// </remarks>
public class FileStorageProviderContractTests
{
    /// <summary>
    /// 内置提供程序都继承自统一的抽象基类
    /// </summary>
    [Theory]
    [InlineData(typeof(LocalFileStorageProvider))]
    [InlineData(typeof(MinioFileStorageProvider))]
    [InlineData(typeof(AliyunOssStorageProvider))]
    [InlineData(typeof(TencentCosStorageProvider))]
    public void Provider_DerivesFromFileStorageProviderBase(Type providerType)
    {
        Assert.True(providerType.IsSubclassOf(typeof(FileStorageProviderBase)));
        Assert.True(typeof(IFileStorageProvider).IsAssignableFrom(providerType));
    }

    /// <summary>
    /// 内置提供程序只暴露一个以对应选项为唯一入参的公开构造函数
    /// </summary>
    /// <remarks>
    /// DI 容器按「参数最多且都能解析」挑构造函数，多出一个可解析的重载就会引入静默的装配歧义，
    /// 因此构造函数数量本身也是契约的一部分。
    /// </remarks>
    [Theory]
    [InlineData(typeof(LocalFileStorageProvider), typeof(LocalStorageOptions))]
    [InlineData(typeof(MinioFileStorageProvider), typeof(MinioStorageOptions))]
    [InlineData(typeof(AliyunOssStorageProvider), typeof(AliyunOssStorageOptions))]
    [InlineData(typeof(TencentCosStorageProvider), typeof(TencentCosStorageOptions))]
    public void Provider_ExposesSingleOptionsConstructor(Type providerType, Type optionsType)
    {
        ConstructorInfo[] constructors = providerType.GetConstructors();

        Assert.Equal(1, constructors.Length);

        ParameterInfo[] parameters = constructors[0].GetParameters();

        Assert.Equal(1, parameters.Length);
        Assert.Equal(typeof(IOptions<>).MakeGenericType(optionsType), parameters[0].ParameterType);
    }

    /// <summary>
    /// 内置提供程序都是可实例化的具体类
    /// </summary>
    [Theory]
    [InlineData(typeof(LocalFileStorageProvider))]
    [InlineData(typeof(MinioFileStorageProvider))]
    [InlineData(typeof(AliyunOssStorageProvider))]
    [InlineData(typeof(TencentCosStorageProvider))]
    public void Provider_IsConcretePublicClass(Type providerType)
    {
        Assert.True(providerType.IsClass);
        Assert.False(providerType.IsAbstract);
        Assert.True(providerType.IsPublic);
    }
}
