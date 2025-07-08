using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TiengAnh.Models;
using TiengAnh.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;

namespace TiengAnh.Controllers
{
    [Authorize] // Ensure only logged-in users can access
    public class FavoritesController : Controller
    {
        private readonly ILogger<FavoritesController> _logger;
        private readonly VocabularyRepository _vocabularyRepository;
        private readonly GrammarRepository _grammarRepository;

        public FavoritesController(
            ILogger<FavoritesController> logger,
            VocabularyRepository vocabularyRepository,
            GrammarRepository grammarRepository)
        {
            _logger = logger;
            _vocabularyRepository = vocabularyRepository;
            _grammarRepository = grammarRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            // Get ID of currently logged in user
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot identify logged in user");
                return RedirectToAction("Login", "Account");
            }
            
            // Get user's favorite data from database
            var vocabularies = await _vocabularyRepository.GetFavoriteVocabulariesAsync(userId);
            var grammars = await _grammarRepository.GetFavoriteGrammarsAsync(userId);
            
            // Set up pagination
            int pageSize = 6;
            int totalVocabularyPages = (int)Math.Ceiling(vocabularies.Count / (double)pageSize);
            int totalGrammarPages = (int)Math.Ceiling(grammars.Count / (double)pageSize);
            
            // Ensure page is valid
            if (page < 1) page = 1;
            
            // Get paginated items
            var paginatedVocabularies = vocabularies
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            var paginatedGrammars = grammars
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            ViewBag.Vocabularies = paginatedVocabularies;
            ViewBag.Grammars = paginatedGrammars;
            ViewBag.CurrentPage = page;
            ViewBag.TotalVocabularyPages = totalVocabularyPages;
            ViewBag.TotalGrammarPages = totalGrammarPages;
            ViewBag.TotalVocabularies = vocabularies.Count;
            ViewBag.TotalGrammars = grammars.Count;
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFavorite([FromBody] ToggleFavoriteModel model)
        {
            // Get ID of currently logged in user
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User is not logged in" });
            }

            bool result = false;
            string message;

            if (model.Type.ToLower() == "grammar")
            {
                // Convert string to int for grammar ID
                if (int.TryParse(model.Id, out int grammarId))
                {
                    result = await _grammarRepository.ToggleFavoriteAsync(grammarId, userId);
                    message = result ? "Grammar favorited successfully" : "Failed to favorite grammar";
                }
                else
                {
                    message = "Invalid grammar ID format";
                }
            }
            else if (model.Type.ToLower() == "vocabulary")
            {
                // Convert string to int for vocabulary ID
                if (int.TryParse(model.Id, out int vocabId))
                {
                    result = await _vocabularyRepository.ToggleFavoriteAsync(vocabId, userId);
                    message = result ? "Vocabulary favorited successfully" : "Failed to favorite vocabulary";
                }
                else
                {
                    message = "Invalid vocabulary ID format";
                }
            }
            else
            {
                message = "Invalid type specified";
            }

            return Ok(new { Success = result, Message = message });
        }

        [HttpPost]
        [Route("RemoveFavorite")]
        public async Task<IActionResult> RemoveFavorite([FromBody] ToggleFavoriteModel model)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Success = false, Message = "User is not logged in" });
            }

            bool result = false;

            if (model.Type.ToLower() == "grammar" && int.TryParse(model.Id, out int grammarId))
            {
                // Use ToggleFavoriteAsync since RemoveFavoriteAsync doesn't exist
                result = await _grammarRepository.ToggleFavoriteAsync(grammarId, userId);
            }
            else if (model.Type.ToLower() == "vocabulary" && int.TryParse(model.Id, out int vocabId))
            {
                // Use ToggleFavoriteAsync since RemoveFavoriteAsync doesn't exist
                result = await _vocabularyRepository.ToggleFavoriteAsync(vocabId, userId);
            }

            return Json(new { 
                Success = result, 
                Message = result ? "Đã xóa khỏi mục yêu thích" : "Không thể xóa khỏi mục yêu thích",
                ItemId = model.Id,
                ItemType = model.Type
            });
        }
    }

    // Model for API to add/remove favorites
    public class ToggleFavoriteModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
