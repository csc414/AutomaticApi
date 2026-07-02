using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace AutomaticApi.Dynamic
{
    internal sealed class DynamicControllerBuilder
    {
        private readonly MethodInfo _emptyArray = typeof(Array).GetTypeInfo().DeclaredMethods.First(o => o.Name.Equals("Empty") && o.IsGenericMethod).MakeGenericMethod(typeof(Object));

        private readonly MethodInfo _createInstance = typeof(ActivatorUtilities).GetTypeInfo().DeclaredMethods.First(o => o.Name.Equals("CreateInstance") && o.IsGenericMethod);

        private readonly AssemblyBuilder _ab;

        private readonly ModuleBuilder _mb;

        private AutomaticApiOptions _options;

        private IServiceProvider _provider;

        private Regex _controllerNameRegex;

        private Regex _nameRegex;

        private Regex _routeRegex;

        /// <summary>
        /// 全局 ControllerAttributes 预编译缓存：每个 lambda 只 Compile() 一次，
        /// 避免在 AddController 中为每个 descriptor 重复编译表达式树。
        /// Item1 = 实例（用于 IRouteTemplateProvider / IApiBehaviorMetadata 判断），Item2 = 用于 Emit 的 builder。
        /// </summary>
        private (Attribute Instance, CustomAttributeBuilder Builder)[] _globalAttrs;

        public DynamicControllerBuilder(string assemblyName)
        {
            AssemblyName name = new AssemblyName(assemblyName);
            _ab = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
            _mb = _ab.DefineDynamicModule(name.Name);
        }

        private void AddController(AutomaticApiDescriptor descriptor)
        {
            var definedType = descriptor.ApiServiceType.GetTypeInfo();
            var implementationType = descriptor.ImplementationType.GetTypeInfo();
            var controllerName = descriptor.ControllerName ?? GetControllerName(definedType.Name);

            if (definedType.Namespace != null)
                controllerName = $"{definedType.Namespace}.{controllerName}";

            if (_mb.GetType(controllerName) != null)
                return;

            var controllerBuilder = _mb.DefineType(controllerName, TypeAttributes.Public, descriptor.ControllerBaseType ?? _options.ControllerBaseType, new[] { definedType });

            // descriptor 级属性每个 lambda 只编译/解析一次，同时拿到实例（用于判断）和 builder（用于 Emit）。
            var descriptorAttrs = CompileAttributes(descriptor.ControllerAttributes);

            var typeAttributes = new List<Attribute>(
                definedType.GetInterfaces().SelectMany(o => o.GetCustomAttributes())
                    .Concat(definedType.GetCustomAttributes()));
            typeAttributes.AddRange(descriptorAttrs.Select(o => o.Instance));
            if (!descriptor.SuppressGlobalControllerAttributes)
                typeAttributes.AddRange(_globalAttrs.Select(o => o.Instance));

            var typeAttributeDatas = definedType.GetInterfaces().SelectMany(o => o.GetCustomAttributesData()).Concat(definedType.GetCustomAttributesData()).ToArray();
            foreach (var attrData in typeAttributeDatas)
            {
                controllerBuilder.SetCustomAttribute(CreateAttribute(attrData));
            }

            foreach (var (_, builder) in descriptorAttrs)
            {
                if (builder != null)
                    controllerBuilder.SetCustomAttribute(builder);
            }

            if (!descriptor.SuppressGlobalControllerAttributes)
            {
                foreach (var (_, builder) in _globalAttrs)
                {
                    if (builder != null)
                        controllerBuilder.SetCustomAttribute(builder);
                }
            }

            if (!descriptor.SuppressDefaultRouteTemplate && !string.IsNullOrWhiteSpace(_options.DefaultRouteTemplate) && !typeAttributes.Any(o => o is IRouteTemplateProvider p && p.Template != null))
            {
                controllerBuilder.SetCustomAttribute(CreateAttribute<RouteAttribute>(_options.DefaultRouteTemplate));
            }

            if (!descriptor.SuppressApiBehavior && _options.UseApiBehavior && !typeAttributes.Any(o => o is IApiBehaviorMetadata))
            {
                controllerBuilder.SetCustomAttribute(CreateAttribute<ApiControllerAttribute>());
            }

            var serviceField = controllerBuilder.DefineField("_service", definedType, FieldAttributes.Private);

            var service = _provider.GetService(definedType);
            var parameterType = service == null ? typeof(IServiceProvider) : definedType;
            var ctorBuilder = controllerBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { parameterType });
            var ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            if (service == null)
            {
                ctorIL.Emit(OpCodes.Call, _emptyArray);
                ctorIL.Emit(OpCodes.Call, _createInstance.MakeGenericMethod(implementationType));
            }
            ctorIL.Emit(OpCodes.Stfld, serviceField);
            ctorIL.Emit(OpCodes.Ret);

            var methods = definedType.GetTypeInfo().DeclaredMethods.Concat(definedType.GetInterfaces().SelectMany(o => o.GetTypeInfo().DeclaredMethods)).ToArray();
            var supressMethods = new HashSet<MethodInfo>();
            var supressMethodAttr = definedType.GetCustomAttribute<SupressMethodAttribute>();
            if (supressMethodAttr != null)
                supressMethods = methods.Where(o => supressMethodAttr.MethodNames.Contains(o.Name)).ToHashSet();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var methodBuilder = controllerBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, method.ReturnType, parameters.Select(o => o.ParameterType).ToArray());

                foreach (var parameter in parameters)
                {
                    var parameterBuilder = methodBuilder.DefineParameter(parameter.Position + 1, parameter.Attributes, parameter.Name);
                    var parameterAttrDatas = parameter.GetCustomAttributesData();
                    foreach (var attr in parameterAttrDatas)
                        parameterBuilder.SetCustomAttribute(CreateAttribute(attr));
                }              
                
                var attrDatas = method.GetCustomAttributesData();
                foreach (var attr in attrDatas)
                    methodBuilder.SetCustomAttribute(CreateAttribute(attr));

                if ((descriptor.SuppressMethods.Contains(method) || supressMethods.Contains(method) || method.GetCustomAttribute<SupressMethodAttribute>() != null) && method.GetCustomAttribute<NonActionAttribute>() == null)
                    methodBuilder.SetCustomAttribute(CreateAttribute<NonActionAttribute>());

                var methodIL = methodBuilder.GetILGenerator();
                methodIL.Emit(OpCodes.Ldarg_0);
                methodIL.Emit(OpCodes.Ldfld, serviceField);
                foreach (var parameter in parameters)
                    methodIL.Emit(OpCodes.Ldarg, parameter.Position + 1);
                methodIL.Emit(OpCodes.Callvirt, method);
                methodIL.Emit(OpCodes.Ret);

                #region Routing
                var methodAttrbutes = method.GetCustomAttributes();
                bool hasRoute = methodAttrbutes.Any(o => o is IRouteTemplateProvider p && p.Template != null);
                bool hasHttpMethod = methodAttrbutes.Any(o => o is IActionHttpMethodProvider);
                var match = _nameRegex.Match(method.Name);
                var httpMethod = "POST";
                if (match.Success)
                {
                    if (match.Groups[1].Success)
                        _options.HttpMethodVerbs.TryGetValue(match.Groups[1].Value, out httpMethod);

                    if (!hasRoute)
                    {
                        if (match.Groups[2].Success)
                        {
                            var matchs = _routeRegex.Matches(match.Groups[2].Value);
                            var route = default(string);
                            if (matchs.Count > 0)
                                route = string.Join("_", matchs.Cast<Match>().Select(o => o.Value));

                            if (parameters.Any(o => o.Name.Equals("id", StringComparison.Ordinal)))
                            {
                                if (route == null)
                                    route = "{id}";
                                else
                                    route = $"{{id}}/{route}";
                            }

                            if (route != null)
                            {
                                methodBuilder.SetCustomAttribute(CreateAttribute<RouteAttribute>(route));
                                hasRoute = true;
                            }
                        }
                    }
                }

                if (!hasHttpMethod)
                    methodBuilder.SetCustomAttribute(CreateAttribute(typeof(AutomaticApiHttpMethodAttribute), httpMethod));

                #endregion
            }

            controllerBuilder.CreateType();
        }

        public void AddControllersFromOptions(AutomaticApiOptions options, IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            _options = options;

            _provider = scope.ServiceProvider;

            _controllerNameRegex = new Regex($"^(?:I)(.+?)(?:{string.Join("|", options.AllowedNameSuffixes)})?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            _nameRegex = new Regex($"^({string.Join("|", options.HttpMethodVerbs.Keys)})?(.*?)(?:Async)?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            _routeRegex = new Regex("[A-Z]{0,1}[a-z0-9]+", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            // 全局属性一次性编译缓存，供每个 AddController 复用。
            _globalAttrs = CompileAttributes(_options.ControllerAttributes).ToArray();

            foreach (var descriptor in _options.AllowedDescriptors)
                AddController(descriptor);
        }

        public Assembly GetAssembly() => _ab;

        CustomAttributeBuilder CreateAttribute<T>(params object[] args) where T : Attribute
        {
            return CreateAttribute(typeof(T), args);
        }

        CustomAttributeBuilder CreateAttribute(Type type, params object[] args)
        {
            ConstructorInfo constructorInfo = type.GetConstructor(args.Select(o => o.GetType()).ToArray());
            return new CustomAttributeBuilder(constructorInfo, args);
        }

        CustomAttributeBuilder CreateAttribute(CustomAttributeData attrData)
        {
            var fields = attrData.NamedArguments.Where(o => o.IsField);
            var properties = attrData.NamedArguments.Where(o => !o.IsField);
            return new CustomAttributeBuilder(attrData.Constructor, attrData.ConstructorArguments.Select(o => {
                if(o.Value is ReadOnlyCollection<CustomAttributeTypedArgument> args)
                    return args.Select(o => o.Value).ToArray();

                return o.Value;
            }).ToArray(), properties.Select(o => (PropertyInfo)o.MemberInfo).ToArray(), properties.Select(o => o.TypedValue.Value).ToArray(), fields.Select(o => (FieldInfo)o.MemberInfo).ToArray(), fields.Select(o => o.TypedValue.Value).ToArray());
        }

        /// <summary>
        /// 将一组属性表达式一次性编译：Compile().Invoke() 得到实例，CreateAttribute 得到 Emit 用的 builder。
        /// 调用方据此避免对同一组表达式重复 Compile 与重复解析。
        /// </summary>
        private (Attribute Instance, CustomAttributeBuilder Builder)[] CompileAttributes(IEnumerable<LambdaExpression> source)
        {
            if (source == null)
                return Array.Empty<(Attribute, CustomAttributeBuilder)>();

            var list = new List<(Attribute, CustomAttributeBuilder)>();
            foreach (var expr in source)
                list.Add(((Attribute)expr.Compile().DynamicInvoke(), CreateAttribute(expr)));
            return list.ToArray();
        }

        CustomAttributeBuilder CreateAttribute(LambdaExpression lambda)
        {
            if (lambda.Body.NodeType != ExpressionType.New && lambda.Body.NodeType != ExpressionType.MemberInit)
                return null;

            var memberInitExp = lambda.Body as MemberInitExpression;
            var newExp = memberInitExp?.NewExpression ?? lambda.Body as NewExpression;
            var constructorArgs = newExp.Arguments.Select(o => GetValue(o)).ToArray();

            if (memberInitExp == null)
                return new CustomAttributeBuilder(newExp.Constructor, constructorArgs);

            var propertyInfos = new PropertyInfo[0];
            var propertyValues = new object[0];
            var fieldInfos = new FieldInfo[0];
            var fieldValues = new object[0];

            foreach (var item in memberInitExp.Bindings.Where(o => o.BindingType == MemberBindingType.Assignment).GroupBy(o => o.Member.MemberType))
            {
                switch (item.Key)
                {
                    case MemberTypes.Property:
                        propertyInfos = item.Select(o => (PropertyInfo)o.Member).ToArray();
                        propertyValues = item.Select(o => GetValue(((MemberAssignment)o).Expression)).ToArray();
                        break;
                    case MemberTypes.Field:
                        fieldInfos = item.Select(o => (FieldInfo)o.Member).ToArray();
                        fieldValues = item.Select(o => GetValue(((MemberAssignment)o).Expression)).ToArray();
                        break;
                }
            }

            return new CustomAttributeBuilder(newExp.Constructor, constructorArgs, propertyInfos, propertyValues, fieldInfos, fieldValues);
        }

        object GetValue(Expression expression)
        {
            if (expression == null)
                return null;

            if (expression.NodeType == ExpressionType.Convert)
                return GetValue(((UnaryExpression)expression).Operand);

            if (expression.NodeType == ExpressionType.Constant)
                return ((ConstantExpression)expression).Value;

            if (expression is MemberExpression memberExpression)
            {
                var obj = GetValue(memberExpression.Expression);
                if (memberExpression.Member is PropertyInfo propertyInfo)
                    return propertyInfo.GetValue(obj);

                if (memberExpression.Member is FieldInfo fieldInfo)
                    return fieldInfo.GetValue(obj);
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                var args = methodCallExpression.Arguments.Select(o => GetValue(o)).ToArray();
                object obj = null;
                if (methodCallExpression.Object != null)
                    obj = GetValue(methodCallExpression.Object);
                return methodCallExpression.Method.Invoke(obj, args);
            }

            if (expression is NewArrayExpression newArrayExpression)
            {
                var args = newArrayExpression.Expressions.Select(o => GetValue(o)).ToArray();
                var ary = (object[])Activator.CreateInstance(newArrayExpression.Type, args.Length);
                for (int i = 0; i < ary.Length; ++i)
                    ary[i] = args[i];
                return ary;
            }

            if (expression is BinaryExpression binaryExpression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Coalesce:
                        {
                            var value = GetValue(binaryExpression.Left);
                            if (value == null)
                                value = GetValue(binaryExpression.Right);
                            return value;
                        }
                    case ExpressionType.ArrayIndex:
                        {
                            var array = (Array)GetValue(binaryExpression.Left);
                            var index = (long)Convert.ChangeType(GetValue(binaryExpression.Right), typeof(long));
                            return array.GetValue(index);
                        }
                }

            }

            throw new NotImplementedException($"NodeType：{expression.NodeType}");
        }

        string GetControllerName(string apiName)
        {
            var match = _controllerNameRegex.Match(apiName);
            string controllerName;
            if (match.Success)
                controllerName = match.Groups[1].Value;
            else
                controllerName = apiName;
            return $"{controllerName}Controller";
        }
    }
}
