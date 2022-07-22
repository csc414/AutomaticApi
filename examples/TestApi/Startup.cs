using Microsoft.OpenApi.Models;
using System.Reflection;
using TestApi.Api;
using TestApi.Services;

namespace TestApi
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerGen(op =>
            {
                op.SwaggerDoc("1.0", new OpenApiInfo { Title = "TestApi 1.0", Version = "1.0" });

                foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
                    op.IncludeXmlComments(path, true);
            });

            services.AddAutomaticApi(op =>
            {
                //op.AddAssembly(Assembly.GetEntryAssembly()); //Generate all api in Assembly

                //op.AddApi<TestService>(); //Generate all api in TestService

                op.AddApi<IDemoAService, TestService>(); //only IDemoAService
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI(op =>
                {
                    op.SwaggerEndpoint("/swagger/1.0/swagger.json", "TestApi 1.0");
                });
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
