using AutomaticApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        private static AutomaticApiOptions _options = new AutomaticApiOptions();

        private static AutomaticApiControllerFeatureProvider _automaticApiControllerFeatureProvider;

        public static IServiceCollection AddAutomaticApi(this IServiceCollection services, Action<AutomaticApiOptions> setupAction)
        {
            setupAction?.Invoke(_options);

            if (_automaticApiControllerFeatureProvider == null)
            {
                _automaticApiControllerFeatureProvider = new(_options);
                services.AddControllers()
                    .PartManager.FeatureProviders.Add(_automaticApiControllerFeatureProvider);
            }
            services.TryAddSingleton(Options.Options.Create(_options));
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IConfigureOptions<MvcOptions>), typeof(AutomaticApiConfigureMvcOptions), ServiceLifetime.Singleton));
            return services;
        }
    }
}
