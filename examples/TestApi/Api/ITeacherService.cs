using AutomaticApi;
using TestApi.Entities;

namespace TestApi.Api
{
    [SupressMethod("InsertAsync", "UpdateAsync")]
    public interface ITeacherService : IGeneralService<Teacher>
    {
        [SupressMethod]
        Task<bool> TeachAsync();
    }
}
