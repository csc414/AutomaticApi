using Microsoft.AspNetCore.Mvc.Filters;
using TestApi.Api;

namespace TestApi.Services
{
    public class TestService : IDemoAService, IDemoBService
    {
        public string Get()
        {
            return "Hello AutomaticApi";
        }

        public Task<string> FetchAsync(Guid id)
        {
            return Task.FromResult($"Hello AutomaticApi {id}");
        }
    }
}
