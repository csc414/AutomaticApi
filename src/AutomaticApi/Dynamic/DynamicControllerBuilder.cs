using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
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

        private void AddAssembly(Assembly assembly)
        {
            var types = assembly.DefinedTypes.Where(o => o.IsClass && !o.IsAbstract && !o.IsGenericType && typeof(IAutomaticApi).IsAssignableFrom(o)).ToArray();
            foreach (var type in types)
                AddImplementationType(type);
        }

        private void AddImplementationType(TypeInfo implementationType)
        {
            var definedInterfaces = implementationType.ImplementedInterfaces.Except
                            (implementationType.ImplementedInterfaces.SelectMany(t => t.GetInterfaces()))
                            .Where(o => typeof(IAutomaticApi).IsAssignableFrom(o)).ToArray();
            foreach (var type in definedInterfaces)
                AddController(type.GetTypeInfo(), implementationType);
        }

        private void AddController(TypeInfo definedType, TypeInfo implementationType)
        {
            var controllerName = GetControllerName(definedType.Name);

            if (definedType.Namespace != null)
                controllerName = $"{definedType.Namespace}.{controllerName}";

            if (_mb.GetType(controllerName) != null)
                return;

            var controllerBuilder = _mb.DefineType(controllerName, TypeAttributes.Public, _options.ControllerBaseType, new[] { definedType });
            var typeAttributes = definedType.GetCustomAttributes(true);
            if (!string.IsNullOrWhiteSpace(_options.DefaultRouteTemplate) && !typeAttributes.Any(o => o is IRouteTemplateProvider p && p.Template != null))
                controllerBuilder.SetCustomAttribute(CreateAttribute<RouteAttribute>(_options.DefaultRouteTemplate));
            if (_options.UseApiBehavior && !typeAttributes.Any(o => o is IApiBehaviorMetadata))
                controllerBuilder.SetCustomAttribute(CreateAttribute<ApiControllerAttribute>());

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

            foreach (var assembly in _options.AllowedAssemblies)
                AddAssembly(assembly);

            foreach (var descriptor in _options.AllowedDescriptors)
            {
                if (descriptor.ApiServiceType == null)
                    AddImplementationType(descriptor.ImplementationType.GetTypeInfo());
                else if (descriptor.ImplementationType != null)
                    AddController(descriptor.ApiServiceType.GetTypeInfo(), descriptor.ImplementationType.GetTypeInfo());
            }
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
