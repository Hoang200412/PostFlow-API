using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IAdminService
    {
        Task<IEnumerable<object>> GetUsersAsync();
        Task<object> CreateUserAsync(CreateUserRequest request);
        Task<object> GetUsageReportAsync(DateTime? date);
        Task<SystemSetting> UpdateSettingAsync(string key, UpdateSettingRequest request);
    }
}
