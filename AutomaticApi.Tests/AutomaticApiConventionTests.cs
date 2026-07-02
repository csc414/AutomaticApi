using AutomaticApi;
using AutomaticApi.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Reflection;

namespace AutomaticApi.Tests;

public class AutomaticApiConventionTests
{
    /// <summary>
    /// 一个实现了 IAutomaticApi 的服务，同时包含一个非接口声明的公共方法。
    /// 后者会成为 MVC 的 action，但不属于任何接口映射，复现 methodMaps[key] 抛 KeyNotFoundException 的问题。
    /// </summary>
    public interface ISampleApi : IAutomaticApi
    {
        string Get();
    }

    public class SampleApi : ISampleApi
    {
        public string Get() => "ok";

        // 非接口公共方法：不在 InterfaceMap 中
        public string Extra() => "extra";
    }

    private static ApplicationModel BuildApplicationModel()
    {
        var typeInfo = typeof(SampleApi).GetTypeInfo();
        var controllerModel = new ControllerModel(typeInfo, new List<object>());

        foreach (var method in typeInfo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            controllerModel.Actions.Add(new ActionModel(method, new List<object>()));
        }

        var application = new ApplicationModel();
        application.Controllers.Add(controllerModel);
        return application;
    }

    [Fact]
    public void Apply_ShouldNotThrow_WhenActionMethodIsNotInInterfaceMap()
    {
        // 复现：Extra() 不是接口方法，旧实现里 methodMaps[actionModel.ActionMethod] 会抛 KeyNotFoundException
        var convention = new AutomaticApiConvention();
        var application = BuildApplicationModel();

        var ex = Record.Exception(() => convention.Apply(application));

        Assert.Null(ex);
    }

    [Fact]
    public void Apply_ShouldRemapInterfaceActionMethod_ToInterfaceMethodInfo()
    {
        var convention = new AutomaticApiConvention();
        var application = BuildApplicationModel();

        convention.Apply(application);

        var getAction = application.Controllers.First().Actions.Single(a => a.ActionMethod.Name == nameof(ISampleApi.Get));
        Assert.Same(typeof(ISampleApi).GetMethod(nameof(ISampleApi.Get))!, getAction.ActionMethod);
    }

    [Fact]
    public void Apply_ShouldLeaveNonInterfaceActionMethod_Unchanged()
    {
        var convention = new AutomaticApiConvention();
        var application = BuildApplicationModel();
        var extraMethod = typeof(SampleApi).GetMethod(nameof(SampleApi.Extra))!;

        convention.Apply(application);

        var extraAction = application.Controllers.First().Actions.Single(a => a.ActionMethod.Name == nameof(SampleApi.Extra));
        Assert.Same(extraMethod, extraAction.ActionMethod);
    }
}
