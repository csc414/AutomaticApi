using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public AutomaticApiOptions AddAssembly(Assembly assembly, Type controllerBaseType = null)
        {
            CheckControllerBaseType(controllerBaseType);

            var types = assembly.DefinedTypes.Where(o => o.IsClass && !o.IsAbstract && !o.IsGenericType && typeof(IAutomaticApi).IsAssignableFrom(o)).ToArray();
            foreach (var t in types)
                _allowedDescriptors.Add(new AutomaticApiDescriptor(t, controllerBaseType));
            return this;
        }

        /// <summary>
        /// Add Implementation Type.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="controllerBaseType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AutomaticApiOptions AddApi<TImplementation>(Type controllerBaseType = null) where TImplementation : class, IAutomaticApi
        {
            CheckControllerBaseType(controllerBaseType);

            _allowedDescriptors.Add(new AutomaticApiDescriptor(typeof(TImplementation), controllerBaseType));
            return this;
        }

        /// <summary>
        /// Add ApiService Type and Implementation Type.
        /// </summary>
        /// <typeparam name="TApiService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="controllerBaseType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AutomaticApiOptions AddApi<TApiService, TImplementation>(Type controllerBaseType = null) where TApiService : IAutomaticApi where TImplementation : class, TApiService
        {
            var t = typeof(TApiService);
            if (!t.IsInterface)
                throw new ArgumentException($"{nameof(TApiService)} must be a Interface based on IAutomaticApi");

            CheckControllerBaseType(controllerBaseType);

            _allowedDescriptors.Add(new AutomaticApiDescriptor(t, typeof(TImplementation), controllerBaseType));
            return this;
        }

        void CheckControllerBaseType(Type controllerBaseType)
        {
            if (controllerBaseType != null && !typeof(ControllerBase).IsAssignableFrom(controllerBaseType))
                throw new ArgumentException($"{nameof(controllerBaseType)} must based on ControllerBase");
        }
    }
}
