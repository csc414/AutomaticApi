using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AutomaticApi
{
    public class AutomaticApiOptions
    {
        internal AutomaticApiOptions()
        {
            HttpMethodVerbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Get"] = "GET",
                ["Find"] = "GET",
                ["Fetch"] = "GET",
                ["Query"] = "GET",

                ["Post"] = "POST",
                ["Add"] = "POST",
                ["Create"] = "POST",
                ["Insert"] = "POST",

                ["Put"] = "PUT",
                ["Update"] = "PUT",
                ["Edit"] = "PUT",
                ["Modify"] = "PUT",

                ["Delete"] = "DELETE",
                ["Remote"] = "DELETE",

                ["Patch"] = "PATCH"
            };

            AllowedNameSuffixes = new HashSet<string> { "Service", "ApiService", "AutoApiService" };
        }

        private readonly HashSet<AutomaticApiDescriptor> _allowedDescriptors = new();

        /// <summary>
        /// Allowed descriptors.
        /// </summary>
        public IEnumerable<AutomaticApiDescriptor> AllowedDescriptors => _allowedDescriptors;

        /// <summary>
        /// The suffixes of api service name.
        /// By default, `IDemoService`, `IDemoApiService`, `IDemoAutoApiService` The api name of these services will be `/api/demo`.
        /// </summary>
        public ICollection<string> AllowedNameSuffixes { get; }

        /// <summary>
        /// The verb at the start of the method name, that be used in HttpMethod.
        /// </summary>
        public Dictionary<string, string> HttpMethodVerbs { get; }

        /// <summary>
        /// Use Api Behavior, Add <see cref="ApiControllerAttribute"/> to Dynamic Controller.
        /// The default value is `true`.
        /// </summary>
        public bool UseApiBehavior { get; set; } = true;

        /// <summary>
        /// Default RouteTemplate, Add <see cref="RouteAttribute"/> to Dynamic Controller.
        /// The default value is `api/[controller]`.
        /// </summary>
        public string DefaultRouteTemplate { get; set; } = "api/[controller]";

        /// <summary>
        /// Dynamic Controller CustomAttributes
        /// </summary>
        public ICollection<Expression<Func<Attribute>>> ControllerAttributes { get; } = new HashSet<Expression<Func<Attribute>>>();

        /// <summary>
        /// Dynamic Controller parent type.
        /// The default value is <see cref="ControllerBase"/>
        /// </summary>
        public Type ControllerBaseType { get; private set; } = typeof(ControllerBase);

        /// <summary>
        /// Specify the Dynamic Controller parent type.
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        public void UseControllerBaseType<TController>() where TController : ControllerBase
        {
            ControllerBaseType = typeof(TController);
        }

        /// <summary>
        /// Add Assembly.
        /// Generate the Assembly's own Automatic Api.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public AutomaticApiOptions AddAssembly(Assembly assembly, Func<AutomaticApiDescriptor, bool> predicate = null)
        {
            predicate ??= _ => true;
            var types = assembly.DefinedTypes.Where(o => o.IsClass && !o.IsAbstract && !o.IsGenericType && typeof(IAutomaticApi).IsAssignableFrom(o)).ToArray();
            foreach (var t in types)
                AddApi(t, predicate);
            return this;
        }

        /// <summary>
        /// Add Implementation Type.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public AutomaticApiOptions AddApi<TImplementation>(Func<AutomaticApiDescriptor, bool> predicate = null) where TImplementation : class, IAutomaticApi
        {
            AddApi(typeof(TImplementation).GetTypeInfo(), predicate);
            return this;
        }

        void AddApi(TypeInfo implementationType, Func<AutomaticApiDescriptor, bool> predicate = null)
        {
            predicate ??= _ => true;
            var definedInterfaces = implementationType.ImplementedInterfaces.Except
                (implementationType.ImplementedInterfaces.SelectMany(t => t.GetInterfaces()))
                .Where(o => typeof(IAutomaticApi).IsAssignableFrom(o)).ToArray();
            foreach (var type in definedInterfaces)
            {
                var descriptor = new AutomaticApiDescriptor(type, implementationType);
                if (predicate(descriptor))
                {
                    Check(descriptor);
                    _allowedDescriptors.Add(descriptor);
                }
            }
        }

        /// <summary>
        /// Add ApiService Type and Implementation Type.
        /// </summary>
        /// <typeparam name="TApiService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AutomaticApiOptions AddApi<TApiService, TImplementation>(Action<AutomaticApiDescriptor> configure = null) where TApiService : IAutomaticApi where TImplementation : class, TApiService
        {
            var t = typeof(TApiService);
            if (!t.IsInterface)
                throw new ArgumentException($"{nameof(TApiService)} must be a Interface based on IAutomaticApi");
            var descriptor = new AutomaticApiDescriptor(t, typeof(TImplementation));
            configure?.Invoke(descriptor);
            Check(descriptor);
            _allowedDescriptors.Add(descriptor);
            return this;
        }

        void Check(AutomaticApiDescriptor descriptor)
        {
            if (descriptor.ControllerBaseType != null && !typeof(ControllerBase).IsAssignableFrom(descriptor.ControllerBaseType))
                throw new ArgumentException($"{nameof(descriptor.ControllerBaseType)} must based on ControllerBase");
        }
    }
}
