using AutomaticApi;
using Microsoft.AspNetCore.Mvc;

namespace TestApi.Api
{
    public interface IDemoBService : IAutomaticApi
    {
        /// <summary>
        /// DemoB 接口
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<string> FetchAsync(Guid id);
    }
}
