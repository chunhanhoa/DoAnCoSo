using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TiengAnh.Models;
using TiengAnh.Repositories;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace TiengAnh.Controllers
{
    public class GrammarController : Controller
    {
        private readonly ILogger<GrammarController> _logger;
        private readonly GrammarRepository _grammarRepository;

        public GrammarController(
            ILogger<GrammarController> logger,
            GrammarRepository grammarRepository)
        {
            _logger = logger;
            _grammarRepository = grammarRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var groupedLessons = await _grammarRepository.GetGroupedGrammarAsync();
            return View(groupedLessons);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var grammar = await _grammarRepository.GetByGrammarIdAsync(id);
            
            if (grammar == null)
            {
                return NotFound();
            }

            // Kiểm tra trạng thái yêu thích nếu người dùng đã đăng nhập
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                grammar.IsFavorite = grammar.FavoriteByUsers != null && 
                                    grammar.FavoriteByUsers.Contains(userId);
            }

            // Ghi log khi người dùng truy cập vào chi tiết bài học
            try
            {
                _logger.LogInformation($"User accessed grammar lesson: {grammar.Title_NP ?? "Unknown"} (ID: {grammar.ID_NP})");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not log grammar access: {ex.Message}");
            }
            
            // Đảm bảo có danh sách ví dụ để tránh lỗi null reference
            if (grammar.Examples == null)
            {
                grammar.Examples = new List<string>();
            }
            
            return View(grammar);
        }
        
        [HttpGet]
        public async Task<IActionResult> Level(string level)
        {
            var grammars = await _grammarRepository.GetGrammarsByLevelAsync(level);
            ViewBag.Level = level;
            return View(grammars);
        }

        // Thêm endpoint mới để xử lý yêu thích
        [HttpPost]
        [Authorize] // Yêu cầu đăng nhập
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để sử dụng tính năng này" });
            }

            var grammar = await _grammarRepository.GetByGrammarIdAsync(id);
            if (grammar == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bài học ngữ pháp" });
            }

            bool isFavorite = false;
            
            // Khởi tạo danh sách nếu chưa có
            if (grammar.FavoriteByUsers == null)
            {
                grammar.FavoriteByUsers = new List<string>();
            }
            
            // Kiểm tra và cập nhật trạng thái yêu thích
            if (grammar.FavoriteByUsers.Contains(userId))
            {
                // Xóa khỏi danh sách yêu thích
                grammar.FavoriteByUsers.Remove(userId);
                isFavorite = false;
            }
            else
            {
                // Thêm vào danh sách yêu thích
                grammar.FavoriteByUsers.Add(userId);
                isFavorite = true;
            }
            
            // Cập nhật vào database
            await _grammarRepository.UpdateAsync(grammar.Id, grammar);
            
            return Json(new 
            { 
                success = true, 
                isFavorite = isFavorite,
                message = isFavorite ? "Đã thêm vào danh sách yêu thích" : "Đã xóa khỏi danh sách yêu thích" 
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Favorites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy tất cả grammar point
            var allGrammar = await _grammarRepository.GetAllAsync();
            
            // Lọc ra những grammar point được yêu thích bởi người dùng hiện tại
            var favoriteGrammar = allGrammar
                .Where(g => g.FavoriteByUsers != null && g.FavoriteByUsers.Contains(userId))
                .OrderBy(g => g.Level)
                .ToList();
                
            ViewBag.Title = "Ngữ pháp yêu thích";
            
            return View("FavoriteGrammar", favoriteGrammar);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var grammar = await _grammarRepository.GetByGrammarIdAsync(id);
            if (grammar == null)
            {
                return NotFound();
            }

            await _grammarRepository.DeleteAsync(grammar.Id);
            TempData["SuccessMessage"] = "Grammar deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var grammar = await _grammarRepository.GetByGrammarIdAsync(id);
            
            if (grammar == null)
            {
                return NotFound();
            }
            
            return View(grammar);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GrammarModel grammar)
        {
            if (id != grammar.ID_NP)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Đảm bảo các trường quan trọng không bị mất khi cập nhật
                    var existingGrammar = await _grammarRepository.GetByGrammarIdAsync(id);
                    
                    if (existingGrammar == null)
                    {
                        return NotFound();
                    }
                    
                    // Giữ lại Id gốc
                    grammar.Id = existingGrammar.Id;
                    
                    // Đảm bảo các collection không bị null
                    if (grammar.FavoriteByUsers == null)
                    {
                        grammar.FavoriteByUsers = existingGrammar.FavoriteByUsers ?? new List<string>();
                    }
                    
                    if (grammar.Examples == null)
                    {
                        grammar.Examples = existingGrammar.Examples ?? new List<string>();
                    }
                    
                    // Process YouTube URL if provided
                    if (!string.IsNullOrEmpty(grammar.VideoUrl_NP))
                    {
                        // Convert watch URLs to embed format if needed
                        grammar.VideoUrl_NP = ConvertYouTubeUrlToEmbed(grammar.VideoUrl_NP);
                    }
                    
                    await _grammarRepository.UpdateAsync(grammar.Id, grammar);
                    
                    TempData["SuccessMessage"] = "Cập nhật bài học ngữ pháp thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi khi cập nhật ngữ pháp: {ex.Message}");
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi cập nhật. Vui lòng thử lại.");
                }
            }
            
            return View(grammar);
        }
        
        // Helper method to convert YouTube URLs to embed format
        private string ConvertYouTubeUrlToEmbed(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Check if it's already an embed URL
            if (url.Contains("youtube.com/embed/"))
                return url;

            // Handle youtube.com/watch?v= format
            if (url.Contains("youtube.com/watch?v="))
            {
                var videoId = url.Split("v=")[1];
                // Remove any additional parameters
                if (videoId.Contains('&'))
                {
                    videoId = videoId.Split('&')[0];
                }
                return $"https://www.youtube.com/embed/{videoId}";
            }

            // Handle youtu.be/ format
            if (url.Contains("youtu.be/"))
            {
                var videoId = url.Split("youtu.be/")[1];
                // Remove any additional parameters
                if (videoId.Contains('?'))
                {
                    videoId = videoId.Split('?')[0];
                }
                return $"https://www.youtube.com/embed/{videoId}";
            }

            return url; // Return as is if no pattern matched
        }
    }
}
