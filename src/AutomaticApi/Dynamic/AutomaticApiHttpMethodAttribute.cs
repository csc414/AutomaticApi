﻿using Microsoft.AspNetCore.Mvc.Routing;
using System;

namespace AutomaticApi.Dynamic
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class AutomaticApiHttpMethodAttribute : HttpMethodAttribute
    {
        public AutomaticApiHttpMethodAttribute(string httpMethod) : base(new[] { httpMethod })
        {
        }
    }
}
