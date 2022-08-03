using AutomaticApi;

namespace TestApi.Api
{
    public interface IGeneralService<T> : IAutomaticApi
    {
        Task<T> GetAsync();

        Task<bool> InsertAsync(T model);

        Task<bool> UpdateAsync(Guid id, T model);

        Task<bool> DeleteAsync(Guid id);
    }
}
