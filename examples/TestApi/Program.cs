using Microsoft.OpenApi.Models;
using TestApi.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen(op =>
{
    op.SwaggerDoc("v1", new OpenApiInfo { Title = "TestApi", Version = "v1" });

    foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
        op.IncludeXmlComments(path, true);
});

builder.Services.AddAutomaticApi(op =>
{
    op.AddAssembly(typeof(IDemoAService).Assembly);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(op =>
    {
        op.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    });
}

app.MapControllers();

app.Run();
