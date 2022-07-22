using System;
using System.Collections.Generic;
using System.Text;

namespace AutomaticApi.Dynamic
{
    public class AutomaticApiDescriptor
    {
        public AutomaticApiDescriptor(Type implementationType)
        {
            ImplementationType = implementationType;
        }

        public AutomaticApiDescriptor(Type apiServiceType, Type implementationType) : this(implementationType)
        {
            ApiServiceType = apiServiceType;
        }

        public Type ApiServiceType { get; }

        public Type ImplementationType { get; }
    }
}
