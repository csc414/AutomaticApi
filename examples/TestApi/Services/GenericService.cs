using TestApi.Api;

namespace TestApi.Services
{
    public class GenericService<T> : IGeneralService<T> where T : new()
    {
        public Task<bool> DeleteAsync(Guid id)
        {
            return Task.FromResult(true);
        }

        public Task<T> GetAsync()
        {
            return Task.FromResult(new T());
        }

        public Task<bool> InsertAsync(T model)
        {
            return Task.FromResult(true);
        }

        public Task<bool> UpdateAsync(Guid id, T model)
        {
            return Task.FromResult(true);
        }
    }
}
