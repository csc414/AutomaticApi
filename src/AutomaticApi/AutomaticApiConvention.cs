using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace AutomaticApi
{
    public class AutomaticApiConvention : IApplicationModelConvention
    {
        private readonly IServiceProvider _serviceProvider;

        public AutomaticApiConvention(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private Regex _controllerNameRegex;

        private Regex _nameRegex;

        private readonly Regex _routeRegex = new Regex("[A-Z]{0,1}[a-z0-9]+", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

        public void Apply(ApplicationModel application)
        {
            var apiOptions = _serviceProvider.GetRequiredService<IOptions<AutomaticApiOptions>>();
            var mvcOptions = _serviceProvider.GetRequiredService<IOptions<MvcOptions>>();
            var modelMetadataProvider = _serviceProvider.GetRequiredService<IModelMetadataProvider>();

            _controllerNameRegex = new Regex($"^(?:I)(.+?)(?:{string.Join("|", apiOptions.Value.ApiNameSuffixes)})?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            _nameRegex = new Regex($"^({string.Join("|", apiOptions.Value.HttpMethodVerbs.Keys)})?(.*?)(?:Async)?$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

            var addList = new List<ControllerModel>();
            var delList = new List<ControllerModel>();
            foreach (var controller in application.Controllers)
            {
                if (typeof(IAutomaticApi).IsAssignableFrom(controller.ControllerType))
                {
                    var allowed = false;
                    var definedInterfaces = controller.ControllerType.ImplementedInterfaces.Except
                         (controller.ControllerType.ImplementedInterfaces.SelectMany(t => t.GetInterfaces()));
                    if (apiOptions.Value.AllowedAll || apiOptions.Value.AllowedAssemblies.Contains(controller.ControllerType.Assembly))
                        allowed = true;

                    foreach (var definedType in definedInterfaces)
                    {
                        if (allowed || apiOptions.Value.AllowedTypes.Contains(controller.ControllerType) || apiOptions.Value.AllowedTypes.Contains(definedType))
                        {
                            var controllerModel = CreateControllerModel(apiOptions.Value, definedType.GetTypeInfo(), controller.ControllerType);

                            var methods = definedType.GetTypeInfo().DeclaredMethods.Concat(definedType.GetInterfaces().SelectMany(o => o.GetTypeInfo().DeclaredMethods));
                            foreach (var methodInfo in methods)
                            {
                                var actionModel = CreateActionModel(apiOptions.Value, mvcOptions.Value, methodInfo);
                                if (actionModel == null)
                                    continue;

                                actionModel.Controller = controllerModel;
                                controllerModel.Actions.Add(actionModel);

                                foreach (var parameterInfo in actionModel.ActionMethod.GetParameters())
                                {
                                    var parameterModel = CreateParameterModel(modelMetadataProvider, parameterInfo);
                                    if (parameterModel != null)
                                    {
                                        parameterModel.Action = actionModel;
                                        actionModel.Parameters.Add(parameterModel);
                                    }
                                }
                            }
                            addList.Add(controllerModel);
                        }
                    }
                    delList.Add(controller);
                }
            }

            delList.ForEach(o => application.Controllers.Remove(o));
            addList.ForEach(application.Controllers.Add);
        }

        internal ControllerModel CreateControllerModel(AutomaticApiOptions options, TypeInfo typeInfo, TypeInfo controllerType)
        {
            var routeAttributes = typeInfo
                    .GetCustomAttributes(inherit: false)
                    .OfType<IRouteTemplateProvider>()
                    .ToArray();

            var attributes = typeInfo.GetCustomAttributes(inherit: true);

            bool hasApiBehavior = false;
            var filteredAttributes = new List<object>();
            foreach (var attribute in attributes)
            {
                if (attribute is IApiBehaviorMetadata)
                    hasApiBehavior = true;

                if (attribute is not IRouteTemplateProvider)
                {
                    filteredAttributes.Add(attribute);
                }
            }

            if (!hasApiBehavior && options.UseApiBehavior)
                filteredAttributes.Add(new ApiControllerAttribute());

            if (routeAttributes.Length == 0)
                filteredAttributes.Add(new RouteAttribute("api/[controller]"));
            else
                filteredAttributes.AddRange(routeAttributes);

            attributes = filteredAttributes.ToArray();

            var controllerModel = new ControllerModel(controllerType, attributes);

            AddRange(controllerModel.Selectors, CreateSelectors(attributes));

            controllerModel.ControllerName = GetControllerName(typeInfo);

            AddRange(controllerModel.Filters, attributes.OfType<IFilterMetadata>());

            foreach (var routeValueProvider in attributes.OfType<IRouteValueProvider>())
            {
                controllerModel.RouteValues.Add(routeValueProvider.RouteKey, routeValueProvider.RouteValue);
            }

            var apiVisibility = attributes.OfType<IApiDescriptionVisibilityProvider>().FirstOrDefault();
            if (apiVisibility != null)
            {
                controllerModel.ApiExplorer.IsVisible = !apiVisibility.IgnoreApi;
            }

            var apiGroupName = attributes.OfType<IApiDescriptionGroupNameProvider>().FirstOrDefault();
            if (apiGroupName != null)
            {
                controllerModel.ApiExplorer.GroupName = apiGroupName.GroupName;
            }

            return controllerModel;
        }

        private ActionModel CreateActionModel(AutomaticApiOptions options, MvcOptions mvcOptions, MethodInfo methodInfo)
        {
            if (!IsAction(methodInfo))
                return null;

            var attributes = methodInfo.GetCustomAttributes(inherit: true);

            var actionModel = new ActionModel(methodInfo, attributes);

            AddRange(actionModel.Filters, attributes.OfType<IFilterMetadata>());

            var actionName = attributes.OfType<ActionNameAttribute>().FirstOrDefault();
            if (actionName?.Name != null)
                actionModel.ActionName = actionName.Name;
            else
                actionModel.ActionName = CanonicalizeActionName(mvcOptions, methodInfo.Name);

            var apiVisibility = attributes.OfType<IApiDescriptionVisibilityProvider>().FirstOrDefault();
            if (apiVisibility != null)
            {
                actionModel.ApiExplorer.IsVisible = !apiVisibility.IgnoreApi;
            }

            var apiGroupName = attributes.OfType<IApiDescriptionGroupNameProvider>().FirstOrDefault();
            if (apiGroupName != null)
            {
                actionModel.ApiExplorer.GroupName = apiGroupName.GroupName;
            }

            foreach (var routeValueProvider in attributes.OfType<IRouteValueProvider>())
            {
                actionModel.RouteValues.Add(routeValueProvider.RouteKey, routeValueProvider.RouteValue);
            }

            var routeAttributes = methodInfo
                    .GetCustomAttributes(inherit: false)
                    .OfType<IRouteTemplateProvider>()
                    .ToArray();

            var hasHttpMethod = false;
            var applicableAttributes = new List<object>(routeAttributes.Length);
            foreach (var attribute in attributes)
            {
                if (attribute is IActionHttpMethodProvider methodProvider)
                    hasHttpMethod = methodProvider.HttpMethods.Any();

                if (attribute is not IRouteTemplateProvider)
                    applicableAttributes.Add(attribute);
            }

            var httpMethod = "POST";
            var match = _nameRegex.Match(methodInfo.Name);
            if (match.Success)
            {
                if (match.Groups[1].Success)
                    options.HttpMethodVerbs.TryGetValue(match.Groups[1].Value, out httpMethod);

                if (routeAttributes.Count(o => o.Template != null) == 0)
                {
                    if (match.Groups[2].Success)
                    {
                        var matchs = _routeRegex.Matches(match.Groups[2].Value);
                        var route = default(string);
                        if (!string.IsNullOrEmpty(match.Groups[2].Value))
                            route = string.Join("_", matchs.Cast<Match>().Select(o => o.Value));

                        var parameters = methodInfo.GetParameters();
                        if (parameters.Any(o => o.Name.Equals("id", StringComparison.Ordinal)))
                        {
                            if (route == null)
                                route = "{id}";
                            else
                                route = $"{{id}}/{route}";
                        }

                        if (route != null)
                            applicableAttributes.Add(new RouteAttribute(route));
                    }
                }
            }

            applicableAttributes.AddRange(routeAttributes);

            if (!hasHttpMethod)
                applicableAttributes.Add(new InternalHttpMethodAttribute(new[] { httpMethod }));

            AddRange(actionModel.Selectors, CreateSelectors(applicableAttributes));

            return actionModel;
        }

        private bool IsAction(MethodInfo methodInfo)
        {
            if (methodInfo.IsSpecialName)
            {
                return false;
            }

            if (methodInfo.IsDefined(typeof(NonActionAttribute)))
            {
                return false;
            }

            if (methodInfo.IsStatic)
            {
                return false;
            }

            if (methodInfo.IsGenericMethod)
            {
                return false;
            }

            return methodInfo.IsPublic;
        }

        private ParameterModel CreateParameterModel(IModelMetadataProvider modelMetadataProvider, ParameterInfo parameterInfo)
        {
            if (parameterInfo == null)
            {
                throw new ArgumentNullException(nameof(parameterInfo));
            }

            var attributes = parameterInfo.GetCustomAttributes(inherit: true);
            BindingInfo bindingInfo;
            if (modelMetadataProvider is ModelMetadataProvider modelMetadataProviderBase)
            {
                var modelMetadata = modelMetadataProviderBase.GetMetadataForParameter(parameterInfo);
                bindingInfo = BindingInfo.GetBindingInfo(attributes, modelMetadata);
            }
            else
            {
                bindingInfo = BindingInfo.GetBindingInfo(attributes);
            }

            var parameterModel = new ParameterModel(parameterInfo, attributes)
            {
                ParameterName = parameterInfo.Name,
                BindingInfo = bindingInfo,
            };

            return parameterModel;
        }

        private IList<SelectorModel> CreateSelectors(IList<object> attributes)
        {
            var routeProviders = new List<IRouteTemplateProvider>();

            var createSelectorForSilentRouteProviders = false;
            foreach (var attribute in attributes)
            {
                if (attribute is IRouteTemplateProvider routeTemplateProvider)
                {
                    if (IsSilentRouteAttribute(routeTemplateProvider))
                    {
                        createSelectorForSilentRouteProviders = true;
                    }
                    else
                    {
                        routeProviders.Add(routeTemplateProvider);
                    }
                }
            }

            foreach (var routeProvider in routeProviders)
            {
                if (!(routeProvider is IActionHttpMethodProvider))
                {
                    createSelectorForSilentRouteProviders = false;
                    break;
                }
            }

            var selectorModels = new List<SelectorModel>();
            if (routeProviders.Count == 0 && !createSelectorForSilentRouteProviders)
            {
                selectorModels.Add(CreateSelectorModel(route: null, attributes: attributes));
            }
            else
            {
                foreach (var routeProvider in routeProviders)
                {
                    var filteredAttributes = new List<object>();
                    foreach (var attribute in attributes)
                    {
                        if (ReferenceEquals(attribute, routeProvider))
                        {
                            filteredAttributes.Add(attribute);
                        }
                        else if (InRouteProviders(routeProviders, attribute))
                        {
                        }
                        else if (
                            routeProvider is IActionHttpMethodProvider &&
                            attribute is IActionHttpMethodProvider)
                        {
                        }
                        else
                        {
                            filteredAttributes.Add(attribute);
                        }
                    }

                    selectorModels.Add(CreateSelectorModel(routeProvider, filteredAttributes));
                }

                if (createSelectorForSilentRouteProviders)
                {
                    var filteredAttributes = new List<object>();
                    foreach (var attribute in attributes)
                    {
                        if (!InRouteProviders(routeProviders, attribute))
                        {
                            filteredAttributes.Add(attribute);
                        }
                    }

                    selectorModels.Add(CreateSelectorModel(route: null, attributes: filteredAttributes));
                }
            }

            return selectorModels;
        }

        private SelectorModel CreateSelectorModel(IRouteTemplateProvider route, IList<object> attributes)
        {
            var selectorModel = new SelectorModel();
            if (route != null)
            {
                selectorModel.AttributeRouteModel = new AttributeRouteModel(route);
            }

            AddRange(selectorModel.ActionConstraints, attributes.OfType<IActionConstraintMetadata>());
            AddRange(selectorModel.EndpointMetadata, attributes);

            var httpMethods = attributes
                .OfType<IActionHttpMethodProvider>()
                .SelectMany(a => a.HttpMethods)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (httpMethods.Length > 0)
            {
                selectorModel.ActionConstraints.Add(new HttpMethodActionConstraint(httpMethods));
                selectorModel.EndpointMetadata.Add(new HttpMethodMetadata(httpMethods));
            }

            return selectorModel;
        }

        private bool IsSilentRouteAttribute(IRouteTemplateProvider routeTemplateProvider)
        {
            return
                routeTemplateProvider.Template == null &&
                routeTemplateProvider.Order == null &&
                routeTemplateProvider.Name == null;
        }

        private bool InRouteProviders(List<IRouteTemplateProvider> routeProviders, object attribute)
        {
            foreach (var rp in routeProviders)
            {
                if (ReferenceEquals(rp, attribute))
                {
                    return true;
                }
            }

            return false;
        }

        private string CanonicalizeActionName(MvcOptions mvcOptions, string actionName)
        {
            const string Suffix = "Async";

            if (mvcOptions.SuppressAsyncSuffixInActionNames &&
                actionName.EndsWith(Suffix, StringComparison.Ordinal))
            {
                actionName = actionName.Substring(0, actionName.Length - Suffix.Length);
            }

            return actionName;
        }

        private string GetControllerName(TypeInfo typeInfo)
        {
            var match = _controllerNameRegex.Match(typeInfo.Name);
            if (match.Success)
                return match.Groups[1].Value;

            return typeInfo.Name;
        }

        private void AddRange<T>(IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }
    }
}
