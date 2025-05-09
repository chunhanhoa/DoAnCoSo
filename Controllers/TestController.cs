using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using TiengAnh.Models;
using TiengAnh.Repositories;
using System.Linq;

namespace TiengAnh.Controllers
{
    public class TestController : Controller
    {
        private readonly ITestRepository _testRepository;
        private readonly ILogger<TestController> _logger;
        private readonly string _webRootPath;

        public TestController(
            ITestRepository testRepository,
            ILogger<TestController> logger,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _testRepository = testRepository;
            _logger = logger;
            _webRootPath = env.WebRootPath;
        }

        // GET: /Test
        public async Task<IActionResult> Index()
        {
            try
            {
                var tests = await _testRepository.GetAllTestsAsync();
                
                // Check for missing images and set defaults
                foreach (var test in tests)
                {
                    if (!string.IsNullOrEmpty(test.ImageUrl))
                    {
                        string imagePath = Path.Combine(_webRootPath, test.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (!System.IO.File.Exists(imagePath))
                        {
                            test.ImageUrl = "/images/tests/default-test.jpg";
                        }
                    }
                    else
                    {
                        test.ImageUrl = "/images/tests/default-test.jpg";
                    }
                }
                
                return View(tests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tests");
                TempData["Error"] = "Không thể tải danh sách bài kiểm tra. Vui lòng thử lại.";
                return View(new List<TestModel>());
            }
        }

        // GET: /Test/Details/{id}
        [Route("Test/Details/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogWarning("Details called with null or empty ID");
                    return RedirectToAction("Index");
                }

                _logger.LogInformation($"Looking for test with ID: {id}");

                // Get all tests first for debugging and matching
                var allTests = await _testRepository.GetAllTestsAsync();
                _logger.LogInformation($"Found {allTests.Count} total tests");
                _logger.LogInformation($"Available test IDs: {string.Join(", ", allTests.Select(t => t.TestIdentifier))}");

                // Try to find by direct string match on TestIdentifier first (most likely case)
                TestModel test = allTests.FirstOrDefault(t => 
                    t.TestIdentifier == id || 
                    t.Id == id || 
                    t.JsonId == id);
                
                if (test == null)
                {
                    // If that fails, try the repository method
                    test = await _testRepository.GetByStringIdAsync(id);
                }

                if (test == null && int.TryParse(id, out int numericId))
                {
                    // Try to get by numeric ID
                    test = await _testRepository.GetByTestIdAsync(numericId);
                }
                
                if (test == null)
                {
                    _logger.LogWarning($"Test not found with ID: {id}");
                    return NotFound();
                }

                _logger.LogInformation($"Found test: {test.Title} with ID: {test.TestIdentifier}");
                return View(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Details action with ID: {id}");
                return RedirectToAction("Index");
            }
        }

        // GET: /Test/Category/{category}
        public async Task<IActionResult> Category(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Danh mục không hợp lệ.";
                return RedirectToAction("Index");
            }

            try
            {
                var tests = await _testRepository.GetTestsByCategoryAsync(id);
                ViewBag.Category = id;
                return View("Index", tests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving tests for category: {id}");
                TempData["Error"] = "Không thể tải bài kiểm tra cho danh mục này.";
                return RedirectToAction("Index");
            }
        }

        // GET: /Test/Level/{level}
        public async Task<IActionResult> Level(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Trình độ không hợp lệ.";
                return RedirectToAction("Index");
            }

            try
            {
                var tests = await _testRepository.GetTestsByLevelAsync(id);
                ViewBag.Level = id;
                return View("Index", tests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving tests for level: {id}");
                TempData["Error"] = "Không thể tải bài kiểm tra cho trình độ này.";
                return RedirectToAction("Index");
            }
        }

        // GET: /Test/Take/{id}
        [Route("Test/Take/{id}")]
        public async Task<IActionResult> Take(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return RedirectToAction("Index");
                }

                _logger.LogInformation($"Take action - Looking for test with ID: {id}");
                
                // Get all tests first for matching
                var allTests = await _testRepository.GetAllTestsAsync();
                _logger.LogInformation($"Take action - Found {allTests.Count} total tests");
                
                // Try to find by direct string match on TestIdentifier first
                TestModel test = allTests.FirstOrDefault(t => 
                    t.TestIdentifier == id || 
                    t.Id == id || 
                    t.JsonId == id);
                
                if (test == null)
                {
                    // If that fails, try the repository method
                    test = await _testRepository.GetByStringIdAsync(id);
                }

                if (test == null && int.TryParse(id, out int numericId))
                {
                    // Try to find by numeric ID
                    test = await _testRepository.GetByTestIdAsync(numericId);
                }
                
                if (test == null)
                {
                    _logger.LogWarning($"Test not found with ID: {id}");
                    return NotFound();
                }

                _logger.LogInformation($"Found test for taking: {test.Title} with ID: {test.TestIdentifier}");
                return View(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving test for taking: {ex.Message}");
                return RedirectToAction("Index");
            }
        }
    }
}