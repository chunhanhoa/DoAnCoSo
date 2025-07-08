using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Threading.Tasks;
using TiengAnh.Services;
using TiengAnh.Models;

namespace TiengAnh.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HealthCheckController : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<HealthCheckController> _logger;

        public HealthCheckController(MongoDbService mongoDbService, ILogger<HealthCheckController> logger)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                // Kiểm tra kết nối đến MongoDB
                var isMongoDbConnected = await CheckMongoDbConnectionAsync();
                
                // Kiểm tra tình trạng hệ thống
                var memoryUsage = GetMemoryUsage();
                
                return Ok(new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    MongoDbConnected = isMongoDbConnected,
                    MemoryUsageMB = memoryUsage,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Health check failed: {ex.Message}");
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                });
            }
        }

        private async Task<bool> CheckMongoDbConnectionAsync()
        {
            try
            {
                // Thực hiện một truy vấn đơn giản để kiểm tra kết nối
                // Sử dụng kiểu UserModel cụ thể thay vì dynamic để tránh lỗi CS1061
                var collection = _mongoDbService.GetCollection<UserModel>("Users");
                var result = await collection.Find(FilterDefinition<UserModel>.Empty)
                    .Limit(1)
                    .CountDocumentsAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"MongoDB connection check failed: {ex.Message}");
                return false;
            }
        }

        private double GetMemoryUsage()
        {
            // Lấy thông tin về bộ nhớ đang sử dụng (MB)
            double memoryUsage = GC.GetTotalMemory(false) / (1024.0 * 1024.0); // Chỉ định .0 để đảm bảo là double
            return Math.Round(memoryUsage, 2); // Không còn nhầm lẫn với Math.Round(decimal, int)
        }
    }
}
