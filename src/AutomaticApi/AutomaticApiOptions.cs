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
        /// Add Type.
        /// It can be a Interface or Class based on <see cref="IAutomaticApi"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public AutomaticApiOptions AddApi<T>() where T : IAutomaticApi
        {
            var t = typeof(T);
            if (t == typeof(IAutomaticApi))
                throw new ArgumentException($"{t.FullName} can't be a AutomaticApi");

            AllowedTypes.Add(t);
            return this;
        }
    }
}
