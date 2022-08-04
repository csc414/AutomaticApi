using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace AutomaticApi.Dynamic
{
    internal sealed class DynamicControllerBuilder
    {
        private readonly MethodInfo _getServiceOrCreateInstance = typeof(ActivatorUtilities).GetTypeInfo().DeclaredMethods.First(o => o.Name.Equals("GetServiceOrCreateInstance"));

        private readonly AssemblyBuilder _ab;

        private readonly ModuleBuilder _mb;

        private AutomaticApiOptions _options;

        private Regex _controllerNameRegex;

        private Regex _nameRegex;

        private Regex _routeRegex;

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
            var typeAttributes = definedType.GetInterfaces().SelectMany(o => o.GetCustomAttributes())
                .Concat(definedType.GetCustomAttributes())
                .Concat(descriptor.ControllerAttributes.Select(o => o.Compile().Invoke()))
                .ToArray();
            var typeAttributeDatas = definedType.GetInterfaces().SelectMany(o => o.GetCustomAttributesData()).Concat(definedType.GetCustomAttributesData()).ToArray();
            foreach (var attrData in typeAttributeDatas)
            {
                controllerBuilder.SetCustomAttribute(CreateAttribute(attrData));
            }

            if(!descriptor.SuppressGlobalControllerAttributes)
            {
                typeAttributes = typeAttributes.Concat(_options.ControllerAttributes.Select(o => o.Compile().Invoke())).ToArray();

                foreach (var item in _options.ControllerAttributes)
                {
                    var attr = CreateAttribute(item);
                    if(attr != null)
                        controllerBuilder.SetCustomAttribute(attr);
                }
            }

            foreach (var item in descriptor.ControllerAttributes)
            {
                var attr = CreateAttribute(item);
                if (attr != null)
                    controllerBuilder.SetCustomAttribute(attr);
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

            var ctorBuilder = controllerBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(IServiceProvider) });
            var ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Call, _getServiceOrCreateInstance.MakeGenericMethod(implementationType));
            ctorIL.Emit(OpCodes.Stfld, serviceField);
            ctorIL.Emit(OpCodes.Ret);

            var methods = definedType.GetTypeInfo().DeclaredMethods.Concat(definedType.GetInterfaces().SelectMany(o => o.GetTypeInfo().DeclaredMethods)).ToArray();
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

                var methodIL = methodBuilder.GetILGenerator();
                methodIL.Emit(OpCodes.Ldarg_0);
                methodIL.Emit(OpCodes.Ldfld, serviceField);
                foreach (var parameter in parameters)
                    methodIL.Emit(OpCodes.Ldarg, parameter.Position + 1);
                methodIL.Emit(OpCodes.Callvirt, method);
                methodIL.Emit(OpCodes.Ret);

                var attrDatas = method.GetCustomAttributesData();
                foreach (var attr in attrDatas)
                    methodBuilder.SetCustomAttribute(CreateAttribute(attr));

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

        public void AddControllersFromOptions(AutomaticApiOptions options)
        {
            _options = options;

            _controllerNameRegex = new Regex($"^(?:I)(.+?)(?:{string.Join("|", options.AllowedNameSuffixes)})?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            _nameRegex = new Regex($"^({string.Join("|", options.HttpMethodVerbs.Keys)})?(.*?)(?:Async)?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            _routeRegex = new Regex("[A-Z]{0,1}[a-z0-9]+", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

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
            return new CustomAttributeBuilder(attrData.Constructor, attrData.ConstructorArguments.Select(o => o.Value).ToArray(), properties.Select(o => (PropertyInfo)o.MemberInfo).ToArray(), properties.Select(o => o.TypedValue.Value).ToArray(), fields.Select(o => (FieldInfo)o.MemberInfo).ToArray(), fields.Select(o => o.TypedValue.Value).ToArray());
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

            var memberInfos = memberInitExp.Bindings.Where(o => o.Member.MemberType == MemberTypes.Property && o.BindingType == MemberBindingType.Assignment).Select(o => (Property: (PropertyInfo)o.Member, Value: GetValue(((MemberAssignment)o).Expression))).ToArray();
            
            return new CustomAttributeBuilder(newExp.Constructor, constructorArgs, memberInfos.Select(o => o.Property).ToArray(), memberInfos.Select(o => o.Value).ToArray());
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
