using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AutomaticApi
{
    public class AutomaticApiOptions
    {
        public AutomaticApiOptions()
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

            ApiNameSuffixes = new HashSet<string> { "Service", "ApiService", "AutoApiService" };
            AllowedAssemblies = new HashSet<Assembly>();
            AllowedTypes = new HashSet<Type>(); 
        }

        /// <summary>
        /// The suffixes of api service name.
        /// By default, `IDemoService`, `IDemoApiService`, `IDemoAutoApiService` The api name of these services will be `/api/demo`.
        /// </summary>
        public ICollection<string> ApiNameSuffixes { get; }

        /// <summary>
        /// Allowed assemblies
        /// </summary>
        public ICollection<Assembly> AllowedAssemblies { get; }

        /// <summary>
        /// Allowed types
        /// </summary>
        public ICollection<Type> AllowedTypes { get; }

        /// <summary>
        /// The verb at the start of the method name, that be used in HttpMethod
        /// </summary>
        public Dictionary<string, string> HttpMethodVerbs { get; }

        /// <summary>
        /// Allow all Assemblies or Types to generate Automatic Api.
        /// </summary>
        public bool AllowedAll { get; set; }

        /// <summary>
        /// Use Api Behavior, Equals <see cref="ApiControllerAttribute"/>
        /// The default value is `true`
        /// </summary>
        public bool UseApiBehavior { get; set; } = true;

        /// <summary>
        /// Add Assembly.
        /// Generate the Assembly's own Automatic Api.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public AutomaticApiOptions AddAssembly(Assembly assembly)
        {
            AllowedAssemblies.Add(assembly);
            return this;
        }

        /// <summary>
        /// Add Implementation Type.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AutomaticApiOptions AddService<TImplementation>() where TImplementation : class, IAutomaticApi
        {
            var t = typeof(TImplementation);
            if (t.IsInterface)
                throw new ArgumentException($"{nameof(TImplementation)} must be a Class");

            AllowedTypes.Add(t);
            return this;
        }

        /// <summary>
        /// Add ApiService Type and Implementation Type.
        /// </summary>
        /// <typeparam name="TApiService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AutomaticApiOptions AddService<TApiService, TImplementation>() where TApiService : IAutomaticApi where TImplementation : class, TApiService
        {
            var t = typeof(TApiService);
            if (!t.IsInterface)
                throw new ArgumentException($"{nameof(TApiService)} must be a Interface based on IAutomaticApi");

            var tt = typeof(TImplementation);
            if (!tt.IsClass)
                throw new ArgumentException($"{nameof(TImplementation)} must be a Class based on {nameof(TApiService)}");

            AllowedTypes.Add(t);
            return this;
        }
    }
}
