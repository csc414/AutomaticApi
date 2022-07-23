# AutomaticApi

AutomaticApi can automatically generate APIs based on services, just define a REST API interface.

# Installation

| Package | NuGet Stable | Downloads |
| ------- | ------------ | --------- |
| [AutomaticApi](https://www.nuget.org/packages/AutomaticApi/) | [![AutomaticApi](https://img.shields.io/nuget/v/AutomaticApi.svg)](https://www.nuget.org/packages/AutomaticApi/) | [![AutomaticApi](https://img.shields.io/nuget/dt/AutomaticApi.svg)](https://www.nuget.org/packages/AutomaticApi/) |
| [AutomaticApi.Abstraction](https://www.nuget.org/packages/AutomaticApi.Abstraction/) | [![AutomaticApi.Abstraction](https://img.shields.io/nuget/v/AutomaticApi.Abstraction.svg)](https://www.nuget.org/packages/AutomaticApi.Abstraction/) | [![AutomaticApi.Abstraction](https://img.shields.io/nuget/dt/AutomaticApi.Abstraction.svg)](https://www.nuget.org/packages/AutomaticApi.Abstraction/) |

# Quick start

## 1. Write service code

```csharp
public class DemoService
{
    public string Get()
    {
        return "Hello AutomaticApi";
    }
}
```

## 2. Define the exposed REST API interface we want

```csharp
public interface IDemoAService : IAutomaticApi
{
    /// <summary>
    /// DemoA api
    /// </summary>
    /// <returns></returns>
    string Get();
}
```

We can also define another REST API interface.

```csharp
public interface IDemoBService : IAutomaticApi
{
    /// <summary>
    /// DemoB api
    /// </summary>
    /// <returns></returns>
    string Get();
}
```

And implement these interfaces.

```csharp
public class DemoService : IDemoAService, IDemoBService
...
```

## 3. Configure Services

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddAutomaticApi(op =>
    {
        op.AddApi<IDemoAService, TestService>(); //only generate IDemoAService

        op.AddApi<TestService>(); //Generate all api interface in TestService 

        op.AddAssembly(Assembly.GetEntryAssembly()); //Generate all api interface in Assembly
    });
}
```

# What a REST api interface?

You can totally use it as a controller.

```csharp
public interface IDemoService : IAutomaticApi
{
    /// <summary>
    /// DemoB api
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpPost]
    [Route("...")]
    string UpdateAsync(Guid id, [FromBody] RequestModel model);
}
```

However, `[HttpPost]` `[Route]` usually doesn't need to be defined. AutomaticApi will generates `Route` and `HttpMethod` based on the Method name.

As we all know, some `[Attrbute]` may can't put it on interface like this.
``` csharp
[Authorize]
public interface IDemoService : IAutomaticApi
...
```
We can do some customization.
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class MyAuthorizeAttribute : AuthorizeAttribute
{
}
```
If you think this component can help you, please give me a star.