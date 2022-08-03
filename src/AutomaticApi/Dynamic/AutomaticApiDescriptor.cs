using System;
using System.Collections.Generic;
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

        public Type ApiServiceType { get; }

        public Type ImplementationType { get; }

        public string ControllerName { get; set; }

        public Type ControllerBaseType { get; set; }

        public bool SuppressGlobalControllerAttributes { get; set; }

        public bool SuppressDefaultRouteTemplate { get; set; }

        public bool SuppressApiBehavior { get; set; }

        public ICollection<Attribute> ControllerAttributes { get; } = new HashSet<Attribute>();
    }
}
