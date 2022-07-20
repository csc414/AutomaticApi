using Microsoft.AspNetCore.Mvc.Controllers;
using System.Linq;
using System.Reflection;

namespace AutomaticApi
{
    public class AutomaticApiControllerFeatureProvider : ControllerFeatureProvider
    {
        private AutomaticApiOptions _options;

        public AutomaticApiControllerFeatureProvider(AutomaticApiOptions options)
        {
            _options = options;
        }

        protected override bool IsController(TypeInfo typeInfo)
        {
            if (typeof(IAutomaticApi).IsAssignableFrom(typeInfo) && typeInfo.IsPublic && typeInfo.IsClass && !typeInfo.IsAbstract && !typeInfo.IsGenericType)
            {
                if (_options.AllowedAll || _options.AllowedAssemblies.Contains(typeInfo.Assembly) || _options.AllowedTypes.Any(o => o.IsAssignableFrom(typeInfo)))
                    return true;
            }
            return false;
        }
    }
}
