using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutomaticApi.Dynamic
{
    public class MvcOptionsPostConfigureOptions : IPostConfigureOptions<MvcOptions>
    {
        private readonly IServiceProvider _provider;

        public MvcOptionsPostConfigureOptions(IServiceProvider serviceProvider)
        {
            _provider = serviceProvider;
        }

        public void PostConfigure(string name, MvcOptions options)
        {
            ServiceCollectionExtensions.ControllerBuilder.AddControllersFromOptions(ServiceCollectionExtensions.Options, _provider);
        }
    }
}
