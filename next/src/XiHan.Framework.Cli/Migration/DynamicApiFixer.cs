// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-06（委托模式）：标注 <c>[DynamicApi]</c>（自己或基类）的服务类的每个公开实例方法生成一个操作契约，构造函数注入原服务、<c>HandleAsync</c> 只做委托；
/// 原服务类保留，删掉 <c>[DynamicApi]</c> / <c>[Authorize]</c> / <c>[PermissionAuthorize]</c> / <c>[Http*]</c> / <c>[Route]</c> 与 <c>ApplicationServiceBase</c> 基类，并登记为作用域服务。
/// </summary>
internal sealed class DynamicApiFixer : SyntaxFixer
{
    /// <summary>
    /// 从服务类与方法上删掉的特性。
    /// </summary>
    private static readonly HashSet<string> _removedAttributes = new(StringComparer.Ordinal)
    {
        "DynamicApi", "Authorize", "AllowAnonymous", "PermissionAuthorize", "Route", "NonAction", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
    };

    /// <summary>
    /// 不能进操作契约的参数或返回类型（流、文件、ASP.NET 类型）。
    /// </summary>
    private static readonly string[] _unsupportedTypes = ["IActionResult", "ActionResult", "FileResult", "FileContentResult", "FileStreamResult", "PhysicalFileResult", "Stream", "IFormFile", "IFormFileCollection", "HttpResponse", "HttpRequest", "HttpContext", "byte[]"];

    /// <summary>
    /// 来自 <c>ApplicationServiceBase</c> 的成员。
    /// </summary>
    private static readonly HashSet<string> _baseMembers = new(StringComparer.Ordinal) { "Logger", "ServiceProvider", "LazyServiceProvider" };

    /// <inheritdoc />
    public override string Id => "V4-06";

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        var root = CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot();
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var attributes = declaration.AttributeLists.SelectMany(static list => list.Attributes).ToList();
            var dynamicApi = attributes.FirstOrDefault(static attribute => AttributeName(attribute) == "DynamicApi");
            var permission = attributes.FirstOrDefault(static attribute => AttributeName(attribute) == "PermissionAuthorize");
            session.Services.Add(
                declaration.Identifier.ValueText,
                declaration.BaseList?.Types.Select(static type => SimpleName(type.Type)).ToList() ?? [],
                dynamicApi is not null && !IsDisabled(dynamicApi),
                dynamicApi is not null && IsDisabled(dynamicApi),
                attributes.Any(static attribute => AttributeName(attribute) is "Authorize" or "PermissionAuthorize"),
                attributes.Any(static attribute => AttributeName(attribute) == "AllowAnonymous"),
                permission?.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString());
        }
    }

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var catalog = session.Services;
        var touched = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(declaration => catalog.IsDynamicApi(declaration.Identifier.ValueText) || declaration.BaseList?.Types.Any(static type => SimpleName(type.Type) == "ApplicationServiceBase") == true)
            .ToHashSet();
        if (touched.Count == 0)
        {
            return root;
        }

        var markers = new Dictionary<SyntaxNode, List<string>>();
        foreach (var declaration in touched.Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) && !declaration.Modifiers.Any(SyntaxKind.StaticKeyword) && catalog.IsDynamicApi(declaration.Identifier.ValueText)).OrderBy(static declaration => declaration.SpanStart))
        {
            Generate(declaration, root, file, session, markers);
        }

        return (CompilationUnitSyntax)new ServiceRewriter(this, file, session, touched, markers).Visit(root);
    }

    /// <summary>
    /// <c>[DynamicApi(false)]</c> 或 <c>[DynamicApi(IsEnabled = false)]</c>。
    /// </summary>
    private static bool IsDisabled(AttributeSyntax attribute)
        => attribute.ArgumentList?.Arguments.Any(static argument => argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression) && (argument.NameEquals is null || argument.NameEquals.Name.Identifier.ValueText == "IsEnabled")) == true;

    /// <summary>
    /// 类型写法是否属于不支持的类型。
    /// </summary>
    private static bool IsUnsupported(string type) => _unsupportedTypes.Any(unsupported => type == unsupported || type.EndsWith("." + unsupported, StringComparison.Ordinal) || type.StartsWith(unsupported + "<", StringComparison.Ordinal));

    /// <summary>
    /// 去掉限定名前缀。
    /// </summary>
    private static TypeSyntax Unqualified(TypeSyntax type) => type is QualifiedNameSyntax qualified ? Unqualified(qualified.Right) : type;

    /// <summary>
    /// 返回类型 → (响应类型源码或空, 方法形态)。
    /// </summary>
    private static (string? Response, MethodShape Shape) Response(TypeSyntax returnType)
    {
        var type = Unqualified(returnType);
        if (type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return (null, MethodShape.SyncVoid);
        }

        if (type is GenericNameSyntax { Identifier.ValueText: "Task" or "ValueTask" } generic && generic.TypeArgumentList.Arguments.Count == 1)
        {
            return (generic.TypeArgumentList.Arguments[0].ToString(), MethodShape.AsyncResult);
        }

        if (type is IdentifierNameSyntax { Identifier.ValueText: "Task" or "ValueTask" })
        {
            return (null, MethodShape.AsyncVoid);
        }

        return (returnType.ToString(), MethodShape.SyncResult);
    }

    /// <summary>
    /// 记一个要插在节点前的标记。
    /// </summary>
    private void Mark(Dictionary<SyntaxNode, List<string>> markers, SyntaxNode node, string message, MigrationFile file, MigrationSession session)
    {
        if (Markers.Has(node, Id, message))
        {
            return;
        }

        if (!markers.TryGetValue(node, out var list))
        {
            list = [];
            markers[node] = list;
        }

        if (!list.Contains(message, StringComparer.Ordinal))
        {
            list.Add(message);
            session.Marked(Id, file, LineOf(node), message);
        }
    }

    /// <summary>
    /// 为一个服务类的每个公开实例方法生成操作文件，并把服务类登记为作用域服务。
    /// </summary>
    private void Generate(ClassDeclarationSyntax declaration, CompilationUnitSyntax root, MigrationFile file, MigrationSession session, Dictionary<SyntaxNode, List<string>> markers)
    {
        var className = declaration.Identifier.ValueText;
        if (file.ProjectDirectory is null)
        {
            Mark(markers, declaration, CliMessages.MigrateMarkerManualRegistration.L(("name", className)), file, session);
            return;
        }

        session.Changed(Id, file, LineOf(declaration));
        var (projectName, _) = V4ServicesGenerator.Names(file.ProjectDirectory);
        if (!session.OperationProjects.TryGetValue(file.ProjectDirectory, out var project))
        {
            project = new V4OperationProject(projectName, file.NewLine);
            session.OperationProjects[file.ProjectDirectory] = project;
        }

        var ns = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
        // 生成文件沿用服务文件的 using（4.x 的命名空间除外，生成文件写出时已过了 V4-10）
        var usings = root.Usings
            .Where(static directive => directive.Alias is null && directive.StaticKeyword.IsKind(SyntaxKind.None) && directive.Name is not null)
            .Select(static directive => directive.Name!.ToString())
            .Where(V4Packages.IsNextNamespace)
            .ToList();
        if (ns.Length > 0)
        {
            usings.Add(ns);
        }

        var catalog = session.Services;
        var resourceWords = V4Operations.Words(V4Operations.ResourceStem(className));
        var resource = V4Operations.Kebab(resourceWords);
        var resourcePascal = V4Operations.Pascal(resourceWords);
        var serviceInterface = declaration.BaseList?.Types.Select(static type => type.Type.ToString()).FirstOrDefault(type => type == "I" + className);
        var serviceType = serviceInterface ?? className;
        var classAuthorized = catalog.IsAuthorized(className);
        var classAnonymous = catalog.IsAnonymous(className);
        var classPermission = catalog.Permission(className);
        var verbs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.PublicKeyword) || method.Modifiers.Any(SyntaxKind.StaticKeyword) || method.Modifiers.Any(SyntaxKind.OverrideKeyword) || method.ExplicitInterfaceSpecifier is not null)
            {
                continue;
            }

            var attributes = method.AttributeLists.SelectMany(static list => list.Attributes).ToList();
            if (attributes.Any(static attribute => AttributeName(attribute) == "NonAction" || (AttributeName(attribute) == "DynamicApi" && IsDisabled(attribute))))
            {
                continue;
            }

            var methodName = method.Identifier.ValueText;
            var parameters = method.ParameterList.Parameters.ToList();
            var inputs = parameters.Where(static parameter => parameter.Type is null || SimpleName(parameter.Type) != "CancellationToken").ToList();
            var (response, shape) = Response(method.ReturnType);
            var unsupported = method.TypeParameterList is not null
                || inputs.Any(static parameter => parameter.Type is null || parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword) || modifier.IsKind(SyntaxKind.OutKeyword) || modifier.IsKind(SyntaxKind.InKeyword)) || IsUnsupported(parameter.Type.ToString()))
                || (response is not null && IsUnsupported(response));
            if (unsupported)
            {
                Mark(markers, method, CliMessages.MigrateMarkerUnsupportedOperation.L(("name", methodName)), file, session);
                continue;
            }

            // 动词：同一资源内重复的加序号
            var verb = V4Operations.Verb(methodName, resourceWords);
            if (verbs.TryGetValue(verb, out var seen))
            {
                verbs[verb] = seen + 1;
                verb = $"{verb}-{seen + 1}";
                Mark(markers, method, CliMessages.MigrateMarkerDuplicateVerb.L(("verb", verb)), file, session);
            }
            else
            {
                verbs[verb] = 1;
            }

            var operationId = $"{resource}.{verb}";
            try
            {
                OperationId.Validate(operationId);
            }
            catch (ArgumentException)
            {
                Mark(markers, method, CliMessages.MigrateMarkerUnsupportedOperation.L(("name", methodName)), file, session);
                continue;
            }

            // 授权：方法级权限 > 类级权限；[AllowAnonymous] 都不设；只有 [Authorize] 时 Policy = authenticated
            var permissions = attributes.Where(static attribute => AttributeName(attribute) == "PermissionAuthorize").ToList();
            var permission = permissions.FirstOrDefault()?.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? classPermission;
            if (permissions.Count > 1 || permissions.FirstOrDefault()?.ArgumentList?.Arguments.Count > 1)
            {
                Mark(markers, method, CliMessages.MigrateMarkerPermissionSimplified.L(), file, session);
            }

            var methodAuthorized = attributes.Any(static attribute => AttributeName(attribute) == "Authorize");
            var anonymous = attributes.Any(static attribute => AttributeName(attribute) == "AllowAnonymous") || (classAnonymous && !methodAuthorized && permissions.Count == 0);
            string? policy = null;
            if (anonymous)
            {
                permission = null;
            }
            else if (permission is null && (classAuthorized || methodAuthorized))
            {
                policy = "authenticated";
            }

            // 请求：唯一的 Dto / Request / Input 参数直接用，否则生成记录
            var stem = methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > "Async".Length ? methodName[..^"Async".Length] : methodName;
            string requestType;
            string? requestRecord = null;
            var direct = inputs.Count == 1 && inputs[0].Type is { } single && V4Operations.IsRequestType(SimpleName(single));
            if (direct)
            {
                requestType = inputs[0].Type!.ToString();
            }
            else
            {
                requestType = stem + "Request";
                var members = inputs.Select(parameter => $"{parameter.Type} {V4Operations.PropertyName(parameter.Identifier.ValueText)}{(parameter.Default is not null ? " " + parameter.Default : string.Empty)}");
                requestRecord = $"public sealed record {requestType}({string.Join(", ", members)});";
            }

            var arguments = parameters.Select(parameter =>
                parameter.Type is not null && SimpleName(parameter.Type) == "CancellationToken" ? "context.CancellationToken"
                : direct ? "request"
                : "request." + V4Operations.PropertyName(parameter.Identifier.ValueText));
            var call = $"service.{methodName}({string.Join(", ", arguments)})";
            var responseType = response ?? "EmptyResponse";
            if (response is null)
            {
                project.NeedsEmptyResponse = true;
            }

            var newLine = file.NewLine;
            var handler = shape switch
            {
                MethodShape.AsyncResult => $"    public async ValueTask<Result<{responseType}>> HandleAsync({requestType} request, InvocationContext context){newLine}        => await {call}.ConfigureAwait(false);{newLine}",
                MethodShape.AsyncVoid => $"    public async ValueTask<Result<EmptyResponse>> HandleAsync({requestType} request, InvocationContext context){newLine}    {{{newLine}        await {call}.ConfigureAwait(false);{newLine}        return EmptyResponse.Instance;{newLine}    }}{newLine}",
                MethodShape.SyncResult => $"    public ValueTask<Result<{responseType}>> HandleAsync({requestType} request, InvocationContext context){newLine}        => new(Result.Success({call}));{newLine}",
                _ => $"    public ValueTask<Result<EmptyResponse>> HandleAsync({requestType} request, InvocationContext context){newLine}    {{{newLine}        {call};{newLine}        return new(Result.Success(EmptyResponse.Instance));{newLine}    }}{newLine}",
            };
            var operationClass = stem + "Operation";
            var path = Path.Combine(file.ProjectDirectory, "Operations", resourcePascal, operationClass + ".cs");
            session.GeneratedFiles[path] = V4OperationsGenerator.Operation(project, resourcePascal, usings, operationId, permission, policy, operationClass, serviceType, requestType, responseType, handler, requestRecord);
            session.Changed(Id, file, LineOf(method));
            session.Changed(Id, file.Sibling(path), 0);
            project.Types.Add(requestType);
            project.Types.Add(responseType);
            project.Usings.UnionWith(usings);
            project.Usings.Add($"{projectName}.Operations.{resourcePascal}");
        }

        // 原服务类按作用域登记（操作类注入它）
        if (!session.Registrations.TryGetValue(file.ProjectDirectory, out var registrations))
        {
            registrations = [];
            session.Registrations[file.ProjectDirectory] = registrations;
        }

        if (!registrations.Any(registration => registration.Implementation == className))
        {
            registrations.Add(new V4ServiceRegistration(ns, className, "Scoped", serviceInterface is null ? [] : [serviceInterface]));
        }
    }

    /// <summary>
    /// 方法形态。
    /// </summary>
    private enum MethodShape
    {
        /// <summary><c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c>。</summary>
        AsyncResult,

        /// <summary><c>Task</c> / <c>ValueTask</c>。</summary>
        AsyncVoid,

        /// <summary>同步有返回值。</summary>
        SyncResult,

        /// <summary><c>void</c>。</summary>
        SyncVoid,
    }

    /// <summary>
    /// 清理服务类与其抽象基类：删特性、去 <c>ApplicationServiceBase</c>、基类成员的语句留标记、插入生成阶段记下的标记。
    /// </summary>
    private sealed class ServiceRewriter(DynamicApiFixer fixer, MigrationFile file, MigrationSession session, HashSet<ClassDeclarationSyntax> targets, Dictionary<SyntaxNode, List<string>> markers) : CSharpSyntaxRewriter
    {
        /// <summary>
        /// 当前在目标类内的嵌套深度。
        /// </summary>
        private int _depth;

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!targets.Contains(node))
            {
                return base.VisitClassDeclaration(node);
            }

            _depth++;
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            _depth--;
            visited = RemoveAttributes(visited, _removedAttributes.Contains, out var removed);
            Count(removed, node);
            var baseCount = visited.BaseList?.Types.Count ?? 0;
            visited = RemoveBaseTypes(visited, static name => name == "ApplicationServiceBase");
            if ((visited.BaseList?.Types.Count ?? 0) != baseCount)
            {
                session.Changed(fixer.Id, file, LineOf(node));
            }

            return Apply(visited, node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (_depth == 0)
            {
                return base.VisitMethodDeclaration(node);
            }

            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            visited = RemoveAttributes(visited, _removedAttributes.Contains, out var removed);
            Count(removed, node);
            // 用到 Logger / ServiceProvider 的语句留标记（行号取原语句）
            if (node.Body is not null && visited.Body is not null && node.Body.Statements.Count == visited.Body.Statements.Count)
            {
                var statements = visited.Body.Statements.ToList();
                var changed = false;
                for (var index = 0; index < statements.Count; index++)
                {
                    if (!UsesBaseMember(node.Body.Statements[index]))
                    {
                        continue;
                    }

                    var message = CliMessages.MigrateMarkerServiceBaseMember.L();
                    if (Markers.Has(statements[index], fixer.Id, message))
                    {
                        continue;
                    }

                    session.Marked(fixer.Id, file, LineOf(node.Body.Statements[index]), message);
                    statements[index] = Markers.With(statements[index], fixer.Id, message, file.NewLine);
                    changed = true;
                }

                if (changed)
                {
                    visited = visited.WithBody(visited.Body.WithStatements(SyntaxFactory.List(statements)));
                }
            }
            else if (node.ExpressionBody is not null && UsesBaseMember(node.ExpressionBody))
            {
                var message = CliMessages.MigrateMarkerServiceBaseMember.L();
                if (!Markers.Has(visited, fixer.Id, message))
                {
                    session.Marked(fixer.Id, file, LineOf(node), message);
                    visited = Markers.With(visited, fixer.Id, message, file.NewLine);
                }
            }

            return Apply(visited, node);
        }

        /// <summary>
        /// 节点里是否以裸名或 <c>this.</c> 用到基类成员。
        /// </summary>
        private static bool UsesBaseMember(SyntaxNode node)
            => node.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static name => _baseMembers.Contains(name.Identifier.ValueText)
                && (name.Parent is not MemberAccessExpressionSyntax access || access.Expression == name || access.Expression is ThisExpressionSyntax));

        /// <summary>
        /// 删掉的特性每个记一次改动。
        /// </summary>
        private void Count(int removed, SyntaxNode original)
        {
            for (var index = 0; index < removed; index++)
            {
                session.Changed(fixer.Id, file, LineOf(original));
            }
        }

        /// <summary>
        /// 插入生成阶段记下的标记。
        /// </summary>
        private TNode Apply<TNode>(TNode node, SyntaxNode original)
            where TNode : SyntaxNode
        {
            if (!markers.TryGetValue(original, out var messages))
            {
                return node;
            }

            foreach (var message in messages)
            {
                node = Markers.With(node, fixer.Id, message, file.NewLine);
            }

            return node;
        }
    }
}
