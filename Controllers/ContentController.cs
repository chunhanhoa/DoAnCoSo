using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiengAnh.Models;
using TiengAnh.Repositories;
using TiengAnh.Services;

namespace TiengAnh.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ContentController : BaseController
    {
        private readonly ILogger<ContentController> _logger;
        private readonly VocabularyRepository _vocabularyRepository;
        private readonly TopicRepository _topicRepository;
        private readonly GrammarRepository _grammarRepository;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ContentController(
            MongoDbService mongoDbService,
            ILogger<ContentController> logger,
            VocabularyRepository vocabularyRepository,
            TopicRepository topicRepository,
            GrammarRepository grammarRepository,
            IWebHostEnvironment hostEnvironment) : base(mongoDbService)
        {
            _logger = logger;
            _vocabularyRepository = vocabularyRepository;
            _topicRepository = topicRepository;
            _grammarRepository = grammarRepository;
            _hostEnvironment = hostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> AddVocabulary()
        {
            ViewBag.Topics = await _topicRepository.GetAllAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVocabulary(VocabularyModel model, IFormFile? ImageFile)
        {
            // Remove any ModelState errors related to the ImageFile since it's optional
            ModelState.Remove("ImageFile");

            try
            {
                // Load topics for dropdown in case we need to return the view with errors
                ViewBag.Topics = await _topicRepository.GetAllAsync();

                // Remove Image_TV from ModelState to prevent validation errors if null
                ModelState.Remove("Image_TV");

                // Validate input
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Vui lòng điền đầy đủ thông tin bắt buộc: {string.Join(", ", errors)}";
                    return View(model);
                }

                // Process image if provided
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Validate file size and extension
                    if (ImageFile.Length > 2 * 1024 * 1024) // 2MB max
                    {
                        TempData["ErrorMessage"] = "Kích thước hình ảnh không được vượt quá 2MB.";
                        return View(model);
                    }

                    string[] allowedExtensions = { ".jpg", ".jpeg", ".png" };
                    string fileExtension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Chỉ chấp nhận file hình ảnh JPG, JPEG hoặc PNG.";
                        return View(model);
                    }

                    // Process image upload
                    string topicFolderName = model.TopicName.ToLower().Replace(" & ", "_").Replace(" ", "_");
                    string uploadFolder = Path.Combine(_hostEnvironment.WebRootPath, "images", "vocabulary", topicFolderName);

                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    // Create a unique filename
                    string uniqueFileName = $"{model.Word_TV.ToLower()}{fileExtension}";
                    string filePath = Path.Combine(uploadFolder, uniqueFileName);

                    // Save the file
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    // Set the image path
                    model.Image_TV = $"/images/vocabulary/{topicFolderName}/{uniqueFileName}";
                }
                else
                {
                    // Set a default image path or leave it empty based on part of speech
                    string[] noImageTypes = { "adv", "prep", "conj", "det", "interj" };
                    if (noImageTypes.Contains(model.ID_LT))
                    {
                        model.Image_TV = null; // No image needed for these types
                    }
                    else
                    {
                        model.Image_TV = "/images/vocabulary/default.jpg"; // Default image for other types
                    }
                }

                // Get the next ID
                int nextId = await _vocabularyRepository.GetNextIdAsync();
                model.ID_TV = nextId;

                // Set audio path (will be handled by text-to-speech on client side)
                model.Audio_TV = $"/audio/{model.Word_TV.ToLower()}.mp3";

                // Initialize other properties
                model.IsFavorite = false;
                model.FavoriteByUsers = new List<string>();

                // Save to database
                await _vocabularyRepository.CreateAsync(model);

                TempData["SuccessMessage"] = $"Đã thêm từ vựng '{model.Word_TV}' thành công!";
                return RedirectToAction("AddVocabulary");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm từ vựng: {ex.Message}";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult AddGrammar()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddGrammar(GrammarModel grammar)
        {
            if (ModelState.IsValid)
            {
                int nextId = await _grammarRepository.GetNextIdAsync();
                grammar.ID_NP = nextId;
                grammar.TimeUpload_NP = DateTime.Now;
                grammar.FavoriteByUsers = new List<string>();

                if (!string.IsNullOrEmpty(grammar.VideoUrl_NP))
                {
                    grammar.VideoUrl_NP = ConvertYouTubeUrlToEmbed(grammar.VideoUrl_NP);
                }

                if (grammar.Examples != null)
                {
                    grammar.Examples = grammar.Examples.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                }
                else
                {
                    grammar.Examples = new List<string>();
                }

                await _grammarRepository.CreateAsync(grammar);
                TempData["SuccessMessage"] = "Grammar added successfully!";
                return RedirectToAction("Index", "Grammar");
            }
            return View(grammar);
        }

        private string ConvertYouTubeUrlToEmbed(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            if (url.Contains("youtube.com/embed/"))
                return url;

            if (url.Contains("youtube.com/watch?v="))
            {
                var videoId = url.Split("v=")[1];
                if (videoId.Contains('&'))
                {
                    videoId = videoId.Split('&')[0];
                }
                return $"https://www.youtube.com/embed/{videoId}";
            }

            if (url.Contains("youtu.be/"))
            {
                var videoId = url.Split("youtu.be/")[1];
                if (videoId.Contains('?'))
                {
                    videoId = videoId.Split('?')[0];
                }
                return $"https://www.youtube.com/embed/{videoId}";
            }

            return url;
        }
    }
}