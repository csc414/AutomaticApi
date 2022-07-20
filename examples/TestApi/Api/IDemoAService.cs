using AutomaticApi;

namespace TestApi.Api
{
    public interface IDemoAService : IAutomaticApi
    {
        /// <summary>
        /// DemoA 接口
        /// </summary>
        /// <returns></returns>
        string Get();
    }
}
