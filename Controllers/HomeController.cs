using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TiengAnh.Models;
using TiengAnh.Repositories;
using System.Linq;
using TiengAnh.Services;
using System.Collections.Generic;

namespace TiengAnh.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITopicRepository _topicRepository;
        private readonly VocabularyRepository _vocabularyRepository;

        public HomeController(MongoDbService mongoDbService, ILogger<HomeController> logger, 
            ITopicRepository topicRepository, VocabularyRepository vocabularyRepository) 
            : base(mongoDbService)
        {
            _logger = logger;
            _topicRepository = topicRepository;
            _vocabularyRepository = vocabularyRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Tải dữ liệu chủ đề cho trang chủ");
            var topics = await _topicRepository.GetAllAsync();
            
            // Lấy danh sách tất cả từ vựng để đếm
            var allVocabularies = await _vocabularyRepository.GetAllAsync();

            // Cập nhật số lượng từ cho mỗi chủ đề
            foreach (var topic in topics)
            {
                int wordCount = allVocabularies.Count(v => v.ID_CD == topic.ID_CD);
                topic.WordCount = wordCount;
                topic.TotalItems = wordCount;
            }
            
            // Chỉ hiển thị 6 chủ đề đầu tiên trên trang chủ
            var featuredTopics = topics.Take(6).ToList();
            
            ViewBag.Topics = featuredTopics;
            
            return View();
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
