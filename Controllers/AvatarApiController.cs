using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TiengAnh.Repositories;

namespace TiengAnh.Controllers
{
    [Route("avatar")]
    [ApiController]
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

        // GET: api/avatar/get-current
        [HttpGet("get-current")]
        public async Task<IActionResult> GetCurrentUserAvatar([FromQuery] string userId = null)
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
                    return Ok(new { 
                        success = false, 
                        avatarUrl = defaultAvatarPath,
                        message = "User not found"
                    });
                }

                // Check if user has avatar
                if (string.IsNullOrEmpty(user.Avatar))
                {
                    _logger.LogInformation($"GetCurrentUserAvatar: User has no avatar, using default");
                    return Ok(new { 
                        success = true, 
                        avatarUrl = defaultAvatarPath
                    });
                }

                // Ensure avatar path starts with /
                string avatarPath = user.Avatar.StartsWith("/") ? user.Avatar : "/" + user.Avatar;
                
                // Check if file exists
                string physicalPath = Path.Combine(_webRootPath, avatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(physicalPath))
                {
                    _logger.LogWarning($"GetCurrentUserAvatar: Avatar file does not exist at {physicalPath}");
                    return Ok(new { 
                        success = false, 
                        avatarUrl = defaultAvatarPath,
                        message = "Avatar file not found"
                    });
                }

                // Set cache control headers
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                
                return Ok(new { 
                    success = true, 
                    avatarUrl = avatarPath,
                    userId = user.Id,
                    timestamp = DateTime.Now.Ticks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentUserAvatar");
                return StatusCode(500, new { 
                    success = false, 
                    avatarUrl = "/images/default-avatar.png", 
                    message = ex.Message 
                });
            }
        }
    }
}
