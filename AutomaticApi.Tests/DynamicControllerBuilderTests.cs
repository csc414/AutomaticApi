using AutomaticApi;
using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading.Tasks;

namespace AutomaticApi.Tests;

public class DynamicControllerBuilderTests
{
    private static (AutomaticApiOptions options, IServiceProvider provider) Setup(
        Action<AutomaticApiOptions>? configure = null)
    {
        var sc = new ServiceCollection();
        sc.AddScoped<IGreeterService, GreeterService>();
        sc.AddScoped<ICounterService, CounterService>();
        var provider = sc.BuildServiceProvider();

        var options = new AutomaticApiOptions();
        configure?.Invoke(options);
        return (options, provider);
    }

    private static Type BuildAndGet(AutomaticApiOptions options, IServiceProvider provider, string typeName)
    {
        var builder = new DynamicControllerBuilder("Test_" + typeName);
        builder.AddControllersFromOptions(options, provider);
        return builder.GetAssembly().GetType(typeName)!;
    }

    [Fact]
    public void BuildsController_ForRegisteredService()
    {
        var (options, provider) = Setup(o =>
        {
            o.AddApi<IGreeterService, GreeterService>();
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");

        Assert.NotNull(type);
        Assert.True(typeof(IAutomaticApi).IsAssignableFrom(type));
    }

    [Fact]
    public void GlobalControllerAttributes_AreAppliedToEveryController()
    {
        var (options, provider) = Setup(o =>
        {
            o.ControllerAttributes.Add(() => new AuthorizeAttribute());
            o.AddApi<IGreeterService, GreeterService>();
            o.AddApi<ICounterService, CounterService>();
        });

        var greeter = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");
        var counter = BuildAndGet(options, provider, "AutomaticApi.Tests.CounterController");

        Assert.NotNull(greeter.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(counter.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void GlobalControllerAttributes_CompileOnceAcrossDescriptors()
    {
        // 旧实现对每个 descriptor 都重新 Compile().Invoke() 全局属性 → 2 个 descriptor 计数 2；
        // 缓存后应只编译一次 → 计数 1。
        CountedAttribute.Reset();

        var (options, provider) = Setup(o =>
        {
            o.ControllerAttributes.Add(() => new CountedAttribute());
            o.AddApi<IGreeterService, GreeterService>();
            o.AddApi<ICounterService, CounterService>();
        });

        var builder = new DynamicControllerBuilder("CompileOnce");
        builder.AddControllersFromOptions(options, provider);

        Assert.Equal(1, CountedAttribute.Count);
    }

    [Fact]
    public void DescriptorControllerAttributes_AreApplied()
    {
        var (options, provider) = Setup(o =>
        {
            o.AddApi<IGreeterService, GreeterService>(d =>
            {
                d.ControllerAttributes.Add(() => new RouteAttribute("custom"));
            });
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");

        var route = type.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("custom", route!.Template);
    }

    [Fact]
    public void DefaultRouteTemplate_Applied_WhenNoCustomRoute()
    {
        var (options, provider) = Setup(o =>
        {
            o.DefaultRouteTemplate = "api/[controller]";
            o.AddApi<ICounterService, CounterService>();
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.CounterController");

        var route = type.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/[controller]", route!.Template);
    }

    [Fact]
    public void HttpMethodAndRoute_DerivedFromMethodName()
    {
        var (options, provider) = Setup(o =>
        {
            o.DefaultRouteTemplate = "api/[controller]";
            o.AddApi<IGreeterService, GreeterService>();
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");

        var getItem = type.GetMethod(nameof(IGreeterService.GetItem))!;
        var getItemRoute = getItem.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(getItemRoute);
        Assert.Equal("{id}/Item", getItemRoute!.Template);
        Assert.Equal("GET", GetHttpMethod(getItem));

        var hello = type.GetMethod(nameof(IGreeterService.Hello))!;
        var helloRoute = hello.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(helloRoute);
        Assert.Equal("Hello", helloRoute!.Template);
        Assert.Equal("POST", GetHttpMethod(hello));
    }

    [Fact]
    public async Task ControllerInstance_InvokesUnderlyingService()
    {
        var (options, provider) = Setup(o =>
        {
            o.AddApi<IGreeterService, GreeterService>();
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");
        var service = provider.GetRequiredService<IGreeterService>();
        var instance = Activator.CreateInstance(type, service)!;

        var helloResult = (string)type.GetMethod(nameof(IGreeterService.Hello))!.Invoke(instance, new object[] { "world" })!;
        Assert.Equal("hello world", helloResult);

        var countResult = await (Task<int>)type.GetMethod(nameof(IGreeterService.CountAsync))!.Invoke(instance, null)!;
        Assert.Equal(42, countResult);
    }

    [Fact]
    public void SuppressMethods_AddsNonActionAttribute()
    {
        var (options, provider) = Setup(o =>
        {
            o.AddApi<IGreeterService, GreeterService>(d =>
            {
                d.SuppressMethods.Add(typeof(IGreeterService).GetMethod(nameof(IGreeterService.Hello))!);
            });
        });

        var type = BuildAndGet(options, provider, "AutomaticApi.Tests.GreeterController");

        var hello = type.GetMethod(nameof(IGreeterService.Hello))!;
        Assert.NotNull(hello.GetCustomAttribute<NonActionAttribute>());

        var getItem = type.GetMethod(nameof(IGreeterService.GetItem))!;
        Assert.Null(getItem.GetCustomAttribute<NonActionAttribute>());
    }

    private static string GetHttpMethod(MethodInfo method)
    {
        var attr = method.GetCustomAttribute<AutomaticApiHttpMethodAttribute>();
        Assert.NotNull(attr);
        return attr!.HttpMethods.First();
    }
}
