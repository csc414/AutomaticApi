using AutomaticApi;
using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        internal static AutomaticApiOptions Options { get; } = new AutomaticApiOptions();

        internal static DynamicControllerBuilder ControllerBuilder { get; private set; }

        public static IServiceCollection AddAutomaticApi(this IServiceCollection services, Action<AutomaticApiOptions> setupAction)
        {
            if (ControllerBuilder == null)
            {
                ControllerBuilder = new DynamicControllerBuilder("AutomaticApi");
                services
                    .AddControllers(op => op.Conventions.Add(new AutomaticApiConvention()))
                    .AddApplicationPart(ControllerBuilder.GetAssembly());
                services.AddSingleton<IPostConfigureOptions<MvcOptions>, MvcOptionsPostConfigureOptions>();
            }

            setupAction?.Invoke(Options);
            return services;
        }
    }
}
