using AutomaticApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutomaticApi.Tests;

/// <summary>用于断言全局 ControllerAttributes 的 Compile 缓存：构造即计数。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class CountedAttribute : Attribute
{
    private static int _count;
    public static int Count => _count;
    public static void Reset() => Interlocked.Exchange(ref _count, 0);
    public CountedAttribute() => Interlocked.Increment(ref _count);
}

public interface IGreeterService : IAutomaticApi
{
    string Hello(string name);
    string GetItem(int id);
    Task<int> CountAsync();
}

public class GreeterService : IGreeterService
{
    public string Hello(string name) => $"hello {name}";
    public string GetItem(int id) => $"item-{id}";
    public Task<int> CountAsync() => Task.FromResult(42);
}

public interface ICounterService : IAutomaticApi
{
    int Add(int a, int b);
}

public class CounterService : ICounterService
{
    public int Add(int a, int b) => a + b;
}
