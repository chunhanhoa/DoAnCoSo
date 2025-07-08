using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Linq;
using TiengAnh.Models;
using TiengAnh.Repositories;
using TiengAnh.Services;
using System.Text.Json;
using MongoDB.Driver;

namespace TiengAnh.Controllers
{
    [Route("api/v1")]
    [ApiController]
    [Produces("application/json")]
    public class ApiV1Controller : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<ApiV1Controller> _logger;
        private readonly UserRepository _userRepository;
        private readonly VocabularyRepository _vocabularyRepository;
        private readonly GrammarRepository _grammarRepository;
        private readonly TopicRepository _topicRepository;
        private readonly TestRepository _testRepository;
        private readonly UserTestRepository _userTestRepository;
        private readonly ExerciseRepository _exerciseRepository;

        public ApiV1Controller(
            MongoDbService mongoDbService,
            ILogger<ApiV1Controller> logger,
            UserRepository userRepository,
            VocabularyRepository vocabularyRepository,
            GrammarRepository grammarRepository,
            TopicRepository topicRepository,
            TestRepository testRepository,
            UserTestRepository userTestRepository,
            ExerciseRepository exerciseRepository)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
            _userRepository = userRepository;
            _vocabularyRepository = vocabularyRepository;
            _grammarRepository = grammarRepository;
            _topicRepository = topicRepository;
            _testRepository = testRepository;
            _userTestRepository = userTestRepository;
            _exerciseRepository = exerciseRepository;
        }

        #region Health Check & System Info
        
        /// <summary>
        /// Kiểm tra trạng thái hệ thống
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var isMongoConnected = await CheckMongoConnectionAsync();
                var memoryUsage = GetMemoryUsage();
                
                return Ok(new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    MongoDbConnected = isMongoConnected,
                    MemoryUsageMB = memoryUsage,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin tổng quan hệ thống
        /// </summary>
        [HttpGet("system/overview")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SystemOverview()
        {
            try
            {
                var users = await _userRepository.GetAllUsersAsync();
                var totalUsers = users.Count;
                var vocabularies = await _vocabularyRepository.GetAllAsync();
                var totalVocabularies = vocabularies.Count;
                var grammar = await _grammarRepository.GetAllAsync();
                var totalGrammar = grammar.Count;
                var topics = await _topicRepository.GetAllTopicsAsync();
                var totalTopics = topics.Count;
                var tests = await _testRepository.GetAllTestsAsync();
                var totalTests = tests.Count;
                var exercises = await _exerciseRepository.GetAllAsync();
                var totalExercises = exercises.Count;

                return Ok(new
                {
                    TotalUsers = totalUsers,
                    TotalVocabularies = totalVocabularies,
                    TotalGrammar = totalGrammar,
                    TotalTopics = totalTopics,
                    TotalTests = totalTests,
                    TotalExercises = totalExercises,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system overview");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region User Management APIs

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("auth/register")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Register([FromBody] RegisterApiRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState });
                }

                // Kiểm tra email đã tồn tại
                var existingUser = await _userRepository.GetByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new { Message = "Email đã được sử dụng" });
                }

                // Tạo user mới
                var user = new UserModel
                {
                    Email = request.Email,
                    Username = request.Username ?? request.Email.Split('@')[0],
                    UserName = request.Username ?? request.Email.Split('@')[0],
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    FullName = request.FullName,
                    Avatar = "/images/avatar/default.jpg",
                    Role = "User",
                    Roles = new List<string> { "User" },
                    CreatedAt = DateTime.Now,
                    RegisterDate = DateTime.Now,
                    IsVerified = false
                };

                await _mongoDbService.GetCollection<UserModel>("Users").InsertOneAsync(user);
                
                _logger.LogInformation($"New user registered via API: {user.Email}");

                return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, new 
                { 
                    Message = "Đăng ký thành công",
                    UserId = user.Id,
                    Email = user.Email,
                    Username = user.Username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API registration");
                return StatusCode(500, new { Error = "Đã xảy ra lỗi trong quá trình đăng ký" });
            }
        }

        /// <summary>
        /// Đăng nhập
        /// </summary>
        [HttpPost("auth/login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Login([FromBody] LoginApiRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState });
                }

                var user = await _userRepository.GetByEmailAsync(request.Email);
                if (user == null)
                {
                    return Unauthorized(new { Message = "Email hoặc mật khẩu không đúng" });
                }

                // Kiểm tra mật khẩu
                bool isPasswordValid = false;
                try
                {
                    if (user.PasswordHash?.StartsWith("$2") == true)
                    {
                        isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                    }
                    else
                    {
                        isPasswordValid = (user.PasswordHash == request.Password);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Password verification error: {ex.Message}");
                    isPasswordValid = (user.PasswordHash == request.Password);
                }

                if (!isPasswordValid)
                {
                    return Unauthorized(new { Message = "Email hoặc mật khẩu không đúng" });
                }

                // Cập nhật last login
                user.LastLogin = DateTime.Now;
                await _userRepository.UpdateUserAsync(user);

                _logger.LogInformation($"User logged in via API: {user.Email}");

                return Ok(new
                {
                    Message = "Đăng nhập thành công",
                    User = new
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Username = user.Username,
                        FullName = user.FullName,
                        Avatar = user.Avatar,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API login");
                return StatusCode(500, new { Error = "Đã xảy ra lỗi trong quá trình đăng nhập" });
            }
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost("auth/change-password")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordApiRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userRepository.GetByUserIdAsync(userId);

                if (user == null)
                {
                    return BadRequest(new { Message = "Không tìm thấy người dùng" });
                }

                // Kiểm tra mật khẩu hiện tại
                bool isCurrentPasswordValid = false;
                if (user.PasswordHash?.StartsWith("$2") == true)
                {
                    isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);
                }
                else
                {
                    isCurrentPasswordValid = (user.PasswordHash == request.CurrentPassword);
                }

                if (!isCurrentPasswordValid)
                {
                    return BadRequest(new { Message = "Mật khẩu hiện tại không đúng" });
                }

                // Cập nhật mật khẩu mới
                string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                bool updateResult = await _userRepository.UpdatePasswordAsync(userId, newPasswordHash);

                if (updateResult)
                {
                    _logger.LogInformation($"User changed password via API: {user.Email}");
                    return Ok(new { Message = "Đổi mật khẩu thành công" });
                }
                else
                {
                    return BadRequest(new { Message = "Không thể cập nhật mật khẩu" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password via API");
                return StatusCode(500, new { Error = "Đã xảy ra lỗi khi đổi mật khẩu" });
            }
        }

        /// <summary>
        /// Lấy thông tin tất cả người dùng (Admin only)
        /// </summary>
        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(List<UserModel>), 200)]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var users = await _userRepository.GetAllUsersAsync();
                var totalUsers = users.Count;
                var paginatedUsers = users.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    Users = paginatedUsers,
                    TotalCount = totalUsers,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin người dùng hiện tại
        /// </summary>
        [HttpGet("users/current")]
        [Authorize]
        [ProducesResponseType(typeof(UserModel), 200)]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userRepository.GetByUserIdAsync(userId);
                
                if (user == null)
                {
                    return NotFound(new { Message = "Không tìm thấy người dùng" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin người dùng theo ID
        /// </summary>
        [HttpGet("users/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserModel), 200)]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                var user = await _userRepository.GetByUserIdAsync(id);
                
                if (user == null)
                {
                    return NotFound(new { Message = "Không tìm thấy người dùng" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật thông tin người dùng
        /// </summary>
        [HttpPut("users/{id}")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UserModel userModel)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                // Chỉ admin hoặc chính người dùng đó mới có thể cập nhật
                if (!isAdmin && currentUserId != id)
                {
                    return Forbid();
                }

                var result = await _userRepository.UpdateUserAsync(userModel);
                
                if (result)
                {
                    return Ok(new { Message = "Cập nhật thành công" });
                }
                else
                {
                    return BadRequest(new { Message = "Cập nhật thất bại" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa người dùng (Admin only)
        /// </summary>
        [HttpDelete("users/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var result = await _userRepository.DeleteUserAsync(id);
                
                if (result)
                {
                    return Ok(new { Message = "Xóa người dùng thành công" });
                }
                else
                {
                    return BadRequest(new { Message = "Xóa người dùng thất bại" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Vocabulary APIs

        /// <summary>
        /// Lấy tất cả từ vựng
        /// </summary>
        [HttpGet("vocabularies")]
        [ProducesResponseType(typeof(List<VocabularyModel>), 200)]
        public async Task<IActionResult> GetAllVocabularies([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var vocabularies = await _vocabularyRepository.GetAllAsync();
                var totalCount = vocabularies.Count;
                var paginatedVocabs = vocabularies.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    Vocabularies = paginatedVocabs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vocabularies");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy từ vựng theo chủ đề
        /// </summary>
        [HttpGet("vocabularies/topic/{topicId}")]
        [ProducesResponseType(typeof(List<VocabularyModel>), 200)]
        public async Task<IActionResult> GetVocabulariesByTopic(int topicId)
        {
            try
            {
                var vocabularies = await _vocabularyRepository.GetVocabulariesByTopicIdAsync(topicId);
                return Ok(vocabularies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vocabularies by topic");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy từ vựng theo ID
        /// </summary>
        [HttpGet("vocabularies/{id}")]
        [ProducesResponseType(typeof(VocabularyModel), 200)]
        public async Task<IActionResult> GetVocabularyById(int id)
        {
            try
            {
                var vocabulary = await _vocabularyRepository.GetByVocabularyIdAsync(id);
                
                if (vocabulary == null)
                {
                    return NotFound(new { Message = "Không tìm thấy từ vựng" });
                }

                return Ok(vocabulary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vocabulary by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Thêm từ vựng mới (Admin only)
        /// </summary>
        [HttpPost("vocabularies")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(201)]
        public async Task<IActionResult> CreateVocabulary([FromBody] VocabularyModel vocabulary)
        {
            try
            {
                var nextId = await _vocabularyRepository.GetNextIdAsync();
                vocabulary.ID_TV = nextId;
                vocabulary.FavoriteByUsers = new List<string>();

                await _vocabularyRepository.CreateAsync(vocabulary);
                
                return CreatedAtAction(nameof(GetVocabularyById), new { id = vocabulary.ID_TV }, vocabulary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating vocabulary");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật từ vựng (Admin only)
        /// </summary>
        [HttpPut("vocabularies/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> UpdateVocabulary(int id, [FromBody] VocabularyModel vocabulary)
        {
            try
            {
                var existing = await _vocabularyRepository.GetByVocabularyIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { Message = "Không tìm thấy từ vựng" });
                }

                vocabulary.Id = existing.Id;
                vocabulary.ID_TV = id;
                
                await _vocabularyRepository.UpdateAsync(existing.Id, vocabulary);
                
                return Ok(new { Message = "Cập nhật từ vựng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vocabulary");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa từ vựng (Admin only)
        /// </summary>
        [HttpDelete("vocabularies/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> DeleteVocabulary(int id)
        {
            try
            {
                var result = await _vocabularyRepository.DeleteVocabularyAsync(id);
                
                if (result)
                {
                    return Ok(new { Message = "Xóa từ vựng thành công" });
                }
                else
                {
                    return BadRequest(new { Message = "Xóa từ vựng thất bại" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting vocabulary");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Toggle yêu thích từ vựng
        /// </summary>
        [HttpPost("vocabularies/{id}/favorite")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> ToggleVocabularyFavorite(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _vocabularyRepository.ToggleFavoriteAsync(id, userId);
                
                return Ok(new { Success = result, Message = result ? "Đã thêm vào yêu thích" : "Đã xóa khỏi yêu thích" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling vocabulary favorite");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Grammar APIs

        /// <summary>
        /// Lấy tất cả ngữ pháp
        /// </summary>
        [HttpGet("grammar")]
        [ProducesResponseType(typeof(List<GrammarModel>), 200)]
        public async Task<IActionResult> GetAllGrammar([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var grammar = await _grammarRepository.GetAllAsync();
                var totalCount = grammar.Count;
                var paginatedGrammar = grammar.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    Grammar = paginatedGrammar,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grammar");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy ngữ pháp theo level
        /// </summary>
        [HttpGet("grammar/level/{level}")]
        [ProducesResponseType(typeof(List<GrammarModel>), 200)]
        public async Task<IActionResult> GetGrammarByLevel(string level)
        {
            try
            {
                var grammar = await _grammarRepository.GetGrammarsByLevelAsync(level);
                return Ok(grammar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grammar by level");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy ngữ pháp theo ID
        /// </summary>
        [HttpGet("grammar/{id}")]
        [ProducesResponseType(typeof(GrammarModel), 200)]
        public async Task<IActionResult> GetGrammarById(int id)
        {
            try
            {
                var grammar = await _grammarRepository.GetByGrammarIdAsync(id);
                
                if (grammar == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài ngữ pháp" });
                }

                return Ok(grammar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grammar by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Thêm ngữ pháp mới (Admin only)
        /// </summary>
        [HttpPost("grammar")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(201)]
        public async Task<IActionResult> CreateGrammar([FromBody] GrammarModel grammar)
        {
            try
            {
                var nextId = await _grammarRepository.GetNextIdAsync();
                grammar.ID_NP = nextId;
                grammar.TimeUpload_NP = DateTime.Now;
                grammar.FavoriteByUsers = new List<string>();

                await _grammarRepository.CreateAsync(grammar);
                
                return CreatedAtAction(nameof(GetGrammarById), new { id = grammar.ID_NP }, grammar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating grammar");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Toggle yêu thích ngữ pháp
        /// </summary>
        [HttpPost("grammar/{id}/favorite")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> ToggleGrammarFavorite(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _grammarRepository.ToggleFavoriteAsync(id, userId);
                
                return Ok(new { Success = result, Message = result ? "Đã thêm vào yêu thích" : "Đã xóa khỏi yêu thích" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling grammar favorite");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Topic APIs

        /// <summary>
        /// Lấy tất cả chủ đề
        /// </summary>
        [HttpGet("topics")]
        [ProducesResponseType(typeof(List<TopicModel>), 200)]
        public async Task<IActionResult> GetAllTopics()
        {
            try
            {
                var topics = await _topicRepository.GetAllTopicsAsync();
                return Ok(topics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting topics");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chủ đề theo ID
        /// </summary>
        [HttpGet("topics/{id}")]
        [ProducesResponseType(typeof(TopicModel), 200)]
        public async Task<IActionResult> GetTopicById(int id)
        {
            try
            {
                var topic = await _topicRepository.GetByTopicIdAsync(id);
                
                if (topic == null)
                {
                    return NotFound(new { Message = "Không tìm thấy chủ đề" });
                }

                return Ok(topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting topic by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Thêm chủ đề mới (Admin only)
        /// </summary>
        [HttpPost("topics")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(201)]
        public async Task<IActionResult> CreateTopic([FromBody] TopicModel topic)
        {
            try
            {
                await _topicRepository.CreateAsync(topic);
                return CreatedAtAction(nameof(GetTopicById), new { id = topic.ID_CD }, topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating topic");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Test APIs

        /// <summary>
        /// Lấy tất cả bài test
        /// </summary>
        [HttpGet("tests")]
        [ProducesResponseType(typeof(List<TestModel>), 200)]
        public async Task<IActionResult> GetAllTests([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var tests = await _testRepository.GetAllTestsAsync();
                var totalCount = tests.Count;
                var paginatedTests = tests.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    Tests = paginatedTests,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tests");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy test theo category
        /// </summary>
        [HttpGet("tests/category/{category}")]
        [ProducesResponseType(typeof(List<TestModel>), 200)]
        public async Task<IActionResult> GetTestsByCategory(string category)
        {
            try
            {
                var tests = await _testRepository.GetTestsByCategoryAsync(category);
                return Ok(tests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tests by category");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy test theo level
        /// </summary>
        [HttpGet("tests/level/{level}")]
        [ProducesResponseType(typeof(List<TestModel>), 200)]
        public async Task<IActionResult> GetTestsByLevel(string level)
        {
            try
            {
                var tests = await _testRepository.GetTestsByLevelAsync(level);
                return Ok(tests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tests by level");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy test theo ID
        /// </summary>
        [HttpGet("tests/{id}")]
        [ProducesResponseType(typeof(TestModel), 200)]
        public async Task<IActionResult> GetTestById(string id)
        {
            try
            {
                var test = await _testRepository.GetByStringIdAsync(id);
                
                if (test == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài test" });
                }

                return Ok(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting test by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Nộp bài test
        /// </summary>
        [HttpPost("tests/{id}/submit")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SubmitTest(string id, [FromBody] TestSubmissionModel submission)
        {
            try
            {
                var test = await _testRepository.GetByStringIdAsync(id);
                if (test == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài test" });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var vietnamTime = GetVietnamTime();

                // Lưu kết quả test
                var userTest = new UserTestModel
                {
                    UserId = userId,
                    TestId = test.Id,
                    TestTitle = test.Title,
                    TestCategory = test.Category,
                    TestLevel = test.Level,
                    ImageUrl = test.ImageUrl,
                    Score = submission.Score,
                    CorrectCount = submission.CorrectCount,
                    TotalQuestions = test.Questions.Count,
                    TimeTaken = submission.TimeTaken,
                    CompletedAt = vietnamTime
                };

                await _userTestRepository.SaveUserTestAsync(userTest);

                return Ok(new 
                { 
                    Message = "Nộp bài test thành công",
                    Score = submission.Score,
                    CorrectCount = submission.CorrectCount,
                    TotalQuestions = test.Questions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting test");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy lịch sử test của user hiện tại
        /// </summary>
        [HttpGet("tests/history")]
        [Authorize]
        [ProducesResponseType(typeof(List<UserTestModel>), 200)]
        public async Task<IActionResult> GetUserTestHistory()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userTests = await _userTestRepository.GetCompletedTestsByUserIdAsync(userId);
                
                return Ok(userTests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user test history");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Exercise APIs

        /// <summary>
        /// Lấy tất cả bài tập
        /// </summary>
        [HttpGet("exercises")]
        [ProducesResponseType(typeof(List<ExerciseModel>), 200)]
        public async Task<IActionResult> GetAllExercises([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var exercises = await _exerciseRepository.GetAllAsync();
                var totalCount = exercises.Count;
                var paginatedExercises = exercises.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    Exercises = paginatedExercises,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exercises");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy bài tập theo chủ đề
        /// </summary>
        [HttpGet("exercises/topic/{topicId}")]
        [ProducesResponseType(typeof(List<ExerciseModel>), 200)]
        public async Task<IActionResult> GetExercisesByTopic(int topicId)
        {
            try
            {
                var exercises = await _exerciseRepository.GetExercisesByTopicIdAsync(topicId);
                return Ok(exercises);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exercises by topic");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy bài tập theo ID
        /// </summary>
        [HttpGet("exercises/{id}")]
        [ProducesResponseType(typeof(ExerciseModel), 200)]
        public async Task<IActionResult> GetExerciseById(int id)
        {
            try
            {
                var exercise = await _exerciseRepository.GetByExerciseIdAsync(id);
                
                if (exercise == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài tập" });
                }

                return Ok(exercise);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exercise by ID");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra đáp án bài tập
        /// </summary>
        [HttpPost("exercises/{id}/check")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> CheckExerciseAnswer(int id, [FromBody] CheckAnswerRequest request)
        {
            try
            {
                var exercise = await _exerciseRepository.GetByExerciseIdAsync(id);
                if (exercise == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài tập" });
                }

                string correctAnswer = exercise.Answer_BT?.Trim() ?? "";
                string selectedAnswer = request.SelectedAnswer?.Trim() ?? "";
                
                bool isCorrect = string.Equals(selectedAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase);

                return Ok(new
                {
                    IsCorrect = isCorrect,
                    CorrectAnswer = correctAnswer,
                    Explanation = exercise.Explanation_BT
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking exercise answer");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Favorites APIs

        /// <summary>
        /// Lấy danh sách yêu thích của user hiện tại
        /// </summary>
        [HttpGet("favorites")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetUserFavorites()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                var favoriteVocabularies = await _vocabularyRepository.GetFavoriteVocabulariesAsync(userId);
                var favoriteGrammar = await _grammarRepository.GetFavoriteGrammarsAsync(userId);

                return Ok(new
                {
                    Vocabularies = favoriteVocabularies,
                    Grammar = favoriteGrammar,
                    TotalVocabularies = favoriteVocabularies.Count,
                    TotalGrammar = favoriteGrammar.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user favorites");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Statistics APIs

        /// <summary>
        /// Lấy thống kê học tập của user hiện tại
        /// </summary>
        [HttpGet("statistics/learning")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetLearningStatistics()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                var completedTests = await _userTestRepository.GetCompletedTestsByUserIdAsync(userId);
                var favoriteVocabs = await _vocabularyRepository.GetFavoriteVocabulariesAsync(userId);
                var favoriteGrammar = await _grammarRepository.GetFavoriteGrammarsAsync(userId);

                var allTests = await _testRepository.GetAllTestsAsync();
                var totalTests = allTests.Count;
                var averageScore = completedTests.Any() ? completedTests.Average(t => t.Score) : 0;

                return Ok(new
                {
                    CompletedTests = completedTests.Count,
                    TotalTests = totalTests,
                    CompletionPercentage = totalTests > 0 ? (double)completedTests.Count / totalTests * 100 : 0,
                    AverageScore = Math.Round(averageScore, 2),
                    FavoriteVocabularies = favoriteVocabs.Count,
                    FavoriteGrammar = favoriteGrammar.Count,
                    LastActivity = completedTests.Any() ? completedTests.Max(t => t.CompletedAt) : (DateTime?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting learning statistics");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Search APIs

        /// <summary>
        /// Tìm kiếm từ vựng
        /// </summary>
        [HttpGet("search/vocabularies")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SearchVocabularies([FromQuery] string keyword, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    return BadRequest(new { Message = "Từ khóa tìm kiếm không được để trống" });
                }

                var allVocabularies = await _vocabularyRepository.GetAllAsync();
                var searchResults = allVocabularies
                    .Where(v => v.Word_TV.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                               v.Meaning_TV.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .ToList();

                return Ok(new
                {
                    Keyword = keyword,
                    Results = searchResults,
                    TotalFound = searchResults.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching vocabularies");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Tìm kiếm ngữ pháp
        /// </summary>
        [HttpGet("search/grammar")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SearchGrammar([FromQuery] string keyword, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    return BadRequest(new { Message = "Từ khóa tìm kiếm không được để trống" });
                }

                var allGrammar = await _grammarRepository.GetAllAsync();
                var searchResults = allGrammar
                    .Where(g => (!string.IsNullOrEmpty(g.Title_NP) && g.Title_NP.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(g.Content_NP) && g.Content_NP.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    .Take(limit)
                    .ToList();

                return Ok(new
                {
                    Keyword = keyword,
                    Results = searchResults,
                    TotalFound = searchResults.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching grammar");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private async Task<bool> CheckMongoConnectionAsync()
        {
            try
            {
                var collection = _mongoDbService.GetCollection<UserModel>("Users");
                await collection.Find(FilterDefinition<UserModel>.Empty).Limit(1).CountDocumentsAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private double GetMemoryUsage()
        {
            return Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2);
        }

        private DateTime GetVietnamTime()
        {
            try
            {
                TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTime(DateTime.Now, vietnamZone);
            }
            catch
            {
                return DateTime.Now.AddHours(7);
            }
        }

        #endregion
    }

    #region Request Models

    public class RegisterApiRequest
    {
        public string Email { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class LoginApiRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ChangePasswordApiRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CheckAnswerRequest
    {
        public string SelectedAnswer { get; set; } = string.Empty;
    }

    #endregion
}
