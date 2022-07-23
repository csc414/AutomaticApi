using System;
using System.Collections.Generic;
using System.Text;

namespace AutomaticApi.Dynamic
{
    public class AutomaticApiDescriptor
    {
        public AutomaticApiDescriptor(Type implementationType, Type controllerBaseType)
        {
            ImplementationType = implementationType;
            ControllerBaseType = controllerBaseType;
        }

        public AutomaticApiDescriptor(Type apiServiceType, Type implementationType, Type controllerBaseType) : this(implementationType, controllerBaseType)
        {
            ApiServiceType = apiServiceType;
        }

        public Type ApiServiceType { get; }

        public Type ImplementationType { get; }

        public Type ControllerBaseType { get; }
    }
}
