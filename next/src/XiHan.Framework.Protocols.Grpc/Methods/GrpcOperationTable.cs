// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Grpc.Proto;

namespace XiHan.Framework.Protocols.Grpc.Methods;

/// <summary>
/// 登记表里暴露给 gRPC 的操作及其 protobuf 描述与 <c>.proto</c> 文本；构造时一次建成，Schema 缺失或无法映射、方法名不合法或重复、同名消息冲突都在这里失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class GrpcOperationTable
{
    /// <summary>
    /// 从登记表与选项建表；登记表此时必须已定型。
    /// </summary>
    public GrpcOperationTable(OperationRegistry registry, GrpcProtocolOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        var operations = new List<GrpcOperation>();
        var seen = new Dictionary<string, GrpcOperation>(StringComparer.Ordinal);
        foreach (var descriptor in registry.ExposedTo(Expose.Grpc))
        {
            // 内部稳定性的操作只供进程内调用，不对外发布
            if (descriptor.Contract.Stability == Stability.Internal)
            {
                continue;
            }

            // protobuf 的线格式只能从 Schema 推出：没有 Schema 的操作不能经 gRPC 暴露
            if (descriptor.Schema is not { } schema)
            {
                throw new FrameworkException(GrpcErrors.SchemaUnsupported, $"Operation '{descriptor.Id}' is exposed to gRPC but has no OperationSchema; register it through Kernel.Generators (AddGeneratedOperations), pass a schema, or remove Grpc from its Expose.");
            }

            GrpcMethodName method;
            try
            {
                method = options.MethodFor(descriptor.Contract);
            }
            catch (ArgumentException exception)
            {
                throw new FrameworkException(GrpcErrors.InvalidMethodName, $"The gRPC method name of operation '{descriptor.Id}' is not valid: {exception.Message}", exception);
            }

            if (seen.TryGetValue(method.ToString(), out var existing))
            {
                throw new FrameworkException(GrpcErrors.DuplicateMethod, $"gRPC method '{method}' is used by both operation '{existing.Descriptor.Id}' and operation '{descriptor.Id}'; override one with '{GrpcProtocolOptions.ConfigurationSection}:Methods:<id>'.");
            }

            ProtoMessage request;
            ProtoMessage response;
            try
            {
                request = ProtoSchema.FromJsonSchema(schema.Request);
                response = ProtoSchema.FromJsonSchema(schema.Response);
            }
            catch (ProtoException exception)
            {
                throw new FrameworkException(GrpcErrors.SchemaUnsupported, $"The schema of operation '{descriptor.Id}' cannot be mapped to protobuf: {exception.Message}", exception);
            }

            var operation = new GrpcOperation(descriptor, method, request, response);
            seen.Add(method.ToString(), operation);
            operations.Add(operation);
        }

        Operations = operations.OrderBy(static o => o.Method.ToString(), StringComparer.Ordinal).ToList();
        Package = options.Package;
        Proto = ProtoFileBuilder.Build(Operations, Package);
    }

    /// <summary>
    /// 发布的操作，按 <c>Service/Method</c> 排序。
    /// </summary>
    public IReadOnlyList<GrpcOperation> Operations { get; }

    /// <summary>
    /// protobuf 包名；空表示没有包。
    /// </summary>
    public string? Package { get; }

    /// <summary>
    /// 生成的 <c>.proto</c> 文本。
    /// </summary>
    public string Proto { get; }
}
