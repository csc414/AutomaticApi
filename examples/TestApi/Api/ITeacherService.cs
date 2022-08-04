using Microsoft.AspNetCore.Mvc;
using TestApi.Entities;

namespace TestApi.Api
{
    public interface ITeacherService : IGeneralService<Student>
    {
        Task<bool> CopyAsync();
    }
}
