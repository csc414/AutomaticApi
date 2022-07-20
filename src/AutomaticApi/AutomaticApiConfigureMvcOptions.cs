using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;

namespace AutomaticApi
{
    public class AutomaticApiConfigureMvcOptions : IConfigureOptions<MvcOptions>
    {
        private readonly IServiceProvider _serviceProvider;

        public AutomaticApiConfigureMvcOptions(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Configure(MvcOptions options)
        {
            options.Conventions.Add(new AutomaticApiConvention(_serviceProvider));
        }
    }
}
