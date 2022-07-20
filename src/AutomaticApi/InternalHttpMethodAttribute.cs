using Microsoft.AspNetCore.Mvc.Routing;
using System.Collections.Generic;

namespace AutomaticApi
{
    internal class InternalHttpMethodAttribute : HttpMethodAttribute
    {
        public InternalHttpMethodAttribute(IEnumerable<string> httpMethods) : base(httpMethods)
        {
        }

        public InternalHttpMethodAttribute(IEnumerable<string> httpMethods, string template) : base(httpMethods, template)
        {
        }
    }
}
