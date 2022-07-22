using AutomaticApi;
using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        private static readonly AutomaticApiOptions _options = new AutomaticApiOptions();

        internal static DynamicControllerBuilder ControllerBuilder { get; private set; }

        public static IServiceCollection AddAutomaticApi(this IServiceCollection services, Action<AutomaticApiOptions> setupAction)
        {
            if (ControllerBuilder == null)
            {
                ControllerBuilder = new DynamicControllerBuilder("AutomaticApi");
                services
                    .AddControllers(op => op.Conventions.Add(new AutomaticApiConvention()))
                    .AddApplicationPart(ControllerBuilder.GetAssembly());
                services.PostConfigure<MvcOptions>(op => ControllerBuilder.AddControllersFromOptions(_options));
            }

            setupAction?.Invoke(_options);
            return services;
        }
    }
}
