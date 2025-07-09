using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TiengAnh.Repositories;

namespace TiengAnh.Controllers
{
    /// <summary>
    /// Controller quản lý avatar người dùng
    /// </summary>
    [Route("api/avatar")]
    [ApiController]
    [Produces("application/json")]
    [Tags("🖼️ Avatar")]
    public class AvatarApiController : ControllerBase
    {
        private readonly ILogger<AvatarApiController> _logger;
        private readonly UserRepository _userRepository;
        private readonly string _webRootPath;

        public AvatarApiController(
            UserRepository userRepository,
            ILogger<AvatarApiController> logger,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _userRepository = userRepository;
            _logger = logger;
            _webRootPath = env.WebRootPath;
        }

        /// <summary>
        /// Lấy avatar hiện tại của người dùng
        /// </summary>
        /// <param name="userId">ID người dùng (tùy chọn)</param>
        /// <returns>Thông tin avatar của người dùng</returns>
        /// <response code="200">Lấy avatar thành công</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        /// <response code="500">Lỗi server</response>
        [HttpGet("get-current")]
        [ProducesResponseType(typeof(AvatarResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> GetCurrentUserAvatar([FromQuery] string? userId = null)
        {
            try
            {
                string defaultAvatarPath = "/images/default-avatar.png";
                string userIdFromClaims = string.IsNullOrEmpty(userId) 
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : userId;
                
                string userEmail = User.FindFirstValue(ClaimTypes.Email);
                
                _logger.LogInformation($"GetCurrentUserAvatar: Looking up for user ID: {userIdFromClaims}, Email: {userEmail}");
                
                // Get user by ID or email
                var user = await _userRepository.GetByUserIdAsync(userIdFromClaims);
                if (user == null && !string.IsNullOrEmpty(userEmail))
                {
                    user = await _userRepository.GetByEmailAsync(userEmail);
                }

                // Return default if user not found
                if (user == null)
                {
                    _logger.LogWarning($"GetCurrentUserAvatar: User not found for ID: {userIdFromClaims}");
                    return Ok(new AvatarResponse
                    { 
                        Success = false, 
                        AvatarUrl = defaultAvatarPath,
                        Message = "Không tìm thấy người dùng"
                    });
                }

                // Check if user has avatar
                if (string.IsNullOrEmpty(user.Avatar))
                {
                    _logger.LogInformation($"GetCurrentUserAvatar: User has no avatar, using default");
                    return Ok(new AvatarResponse
                    { 
                        Success = true, 
                        AvatarUrl = defaultAvatarPath,
                        Message = "Sử dụng avatar mặc định"
                    });
                }

                // Ensure avatar path starts with /
                string avatarPath = user.Avatar.StartsWith("/") ? user.Avatar : "/" + user.Avatar;
                
                // Check if file exists
                string physicalPath = Path.Combine(_webRootPath, avatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(physicalPath))
                {
                    _logger.LogWarning($"GetCurrentUserAvatar: Avatar file does not exist at {physicalPath}");
                    return Ok(new AvatarResponse
                    { 
                        Success = false, 
                        AvatarUrl = defaultAvatarPath,
                        Message = "File avatar không tồn tại"
                    });
                }

                // Set cache control headers
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                
                return Ok(new AvatarResponse
                { 
                    Success = true, 
                    AvatarUrl = avatarPath,
                    UserId = user.Id,
                    Timestamp = DateTime.Now.Ticks,
                    Message = "Lấy avatar thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentUserAvatar");
                return StatusCode(500, new ErrorResponse
                { 
                    Success = false, 
                    Message = $"Lỗi server: {ex.Message}"
                });
            }
        }
    }

    /// <summary>
    /// Response model cho Avatar API
    /// </summary>
    public class AvatarResponse
    {
        /// <summary>
        /// Trạng thái thành công
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// URL của avatar
        /// </summary>
        public string AvatarUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// ID người dùng
        /// </summary>
        public string? UserId { get; set; }
        
        /// <summary>
        /// Timestamp để cache busting
        /// </summary>
        public long? Timestamp { get; set; }
        
        /// <summary>
        /// Thông báo
        /// </summary>
        public string? Message { get; set; }
    }
}
    /// <summary>
    /// Response model cho lỗi
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Trạng thái thành công (false cho lỗi)
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Thông báo lỗi
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

