using System;
using System.Collections.Generic;

namespace AutomaticApi
{
    /// <summary>
    /// Suppress method
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SupressMethodAttribute : Attribute
    {
        public SupressMethodAttribute()
        {
        }

        public SupressMethodAttribute(params string[] methodNames)
        {
            MethodNames = new HashSet<string>(methodNames);
        }

        public HashSet<string> MethodNames { get; }
    }
}
