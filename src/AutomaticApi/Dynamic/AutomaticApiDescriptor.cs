using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace AutomaticApi.Dynamic
{
    public class AutomaticApiDescriptor
    {
        public AutomaticApiDescriptor(Type apiServiceType, Type implementationType)
        {
            ApiServiceType = apiServiceType;
            ImplementationType = implementationType;
        }

        /// <summary>
        /// ApiService Type
        /// </summary>
        public Type ApiServiceType { get; }

        /// <summary>
        /// Implementation Type
        /// </summary>
        public Type ImplementationType { get; }

        /// <summary>
        /// Controller Name 
        /// </summary>
        public string ControllerName { get; set; }

        /// <summary>
        /// Dynamic Controller parent type, if no definition then use the global options.
        /// </summary>
        public Type ControllerBaseType { get; set; }

        /// <summary>
        /// Dynamic Controller CustomAttributes
        /// </summary>
        public ICollection<Expression<Func<Attribute>>> ControllerAttributes { get; } = new HashSet<Expression<Func<Attribute>>>();

        /// <summary>
        /// Suppress Global Dynamic Controller CustomAttributes
        /// </summary>
        public bool SuppressGlobalControllerAttributes { get; set; }

        /// <summary>
        /// Suppress Global DefaultRouteTemplate
        /// </summary>
        public bool SuppressDefaultRouteTemplate { get; set; }

        /// <summary>
        /// Suppress Global ApiBehavior
        /// </summary>
        public bool SuppressApiBehavior { get; set; }
    }
}
