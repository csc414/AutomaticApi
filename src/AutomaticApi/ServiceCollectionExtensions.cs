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

        public static IServiceCollection AddAutomaticApi(this IServiceCollection services, Action<AutomaticApiOptions> setupAction)
        {
            setupAction?.Invoke(_options);
            services.AddControllers(op => op.Conventions.Add(new AutomaticApiConvention()));
            return services;
        }
    }
}
