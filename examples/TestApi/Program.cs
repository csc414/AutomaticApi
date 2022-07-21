using AutomaticApi;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen(op =>
{
    op.SwaggerDoc("1.0", new OpenApiInfo { Title = "TestApi 1.0", Version = "1.0" });

    foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
        op.IncludeXmlComments(path, true);
});

var controllerBuilder = new DynamicControllerBuilder(new AutomaticApiOptions(), "AutomaticApi");
controllerBuilder.AddAssembly(Assembly.GetEntryAssembly());

builder.Services.AddMvc(op =>
{
    op.Conventions.Add(new AutomaticApiConvention());
})
    .AddApplicationPart(controllerBuilder.GetAssembly());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(op =>
    {
        op.SwaggerEndpoint("/swagger/1.0/swagger.json", "TestApi 1.0");
    });
}

app.MapControllers();

app.Run();
