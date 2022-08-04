using TestApi.Api;
using TestApi.Entities;

namespace TestApi.Services
{
    public class TeacherService : GenericService<Teacher>, ITeacherService
    {
        public Task<bool> TeachAsync()
        {
            return Task.FromResult(true);
        }
    }
}
