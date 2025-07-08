using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using TiengAnh.Models;
using TiengAnh.Repositories;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;

namespace TiengAnh.Controllers
{
    [Authorize] // Add this attribute to require authentication
    public class TestController : Controller
    {
        private readonly ITestRepository _testRepository;
        private readonly UserTestRepository _userTestRepository;
        private readonly ILogger<TestController> _logger;
        private readonly string _webRootPath;

        public TestController(
            ITestRepository testRepository,
            UserTestRepository userTestRepository,
            ILogger<TestController> logger,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _testRepository = testRepository;
            _userTestRepository = userTestRepository;
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
        [HttpGet]
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
        [HttpGet]
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
        [HttpGet]
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
        [HttpGet]
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
                
                // Save current test data to session before clearing TempData
                if (TempData.ContainsKey("UserAnswers") && 
                    TempData.ContainsKey("Score") && 
                    TempData.ContainsKey("CorrectCount") &&
                    TempData.ContainsKey("SelectedQuestionIds"))
                {
                    // Create a dictionary to store all the data we need
                    var savedTestData = new Dictionary<string, string>
                    {
                        ["UserAnswers"] = TempData["UserAnswers"]?.ToString(),
                        ["Score"] = TempData["Score"]?.ToString(),
                        ["CorrectCount"] = TempData["CorrectCount"]?.ToString(),
                        ["TimeTaken"] = TempData["TimeTaken"]?.ToString(),
                        ["CompletedAt"] = TempData["CompletedAt"]?.ToString(),
                        ["SelectedQuestionIds"] = TempData["SelectedQuestionIds"]?.ToString(),
                        ["QuestionOrder"] = TempData["QuestionOrder"]?.ToString()
                    };
                    
                    // Save to session with a key that includes the test ID
                    string sessionKey = $"LastCompletedTest_{id}";
                    HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(savedTestData));
                }
                
                // Clear any existing test results from TempData when starting a new test attempt
                if (TempData.ContainsKey("UserAnswers")) TempData.Remove("UserAnswers");
                if (TempData.ContainsKey("Score")) TempData.Remove("Score");  
                if (TempData.ContainsKey("CorrectCount")) TempData.Remove("CorrectCount");
                if (TempData.ContainsKey("TimeTaken")) TempData.Remove("TimeTaken");
                if (TempData.ContainsKey("TimeUsed")) TempData.Remove("TimeUsed");
                if (TempData.ContainsKey("CompletedAt")) TempData.Remove("CompletedAt");
                if (TempData.ContainsKey("SelectedQuestionIds")) TempData.Remove("SelectedQuestionIds");
                if (TempData.ContainsKey("QuestionOrder")) TempData.Remove("QuestionOrder");
                
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

                var allQuestions = test.Questions.ToList();
                var random = new Random();
                var randomQuestions = allQuestions
                    .OrderBy(q => random.Next())
                    .Take(20)
                    .ToList();
                    
                test.Questions = randomQuestions;
                
                // Store the selected question IDs in TempData for later use
                var selectedQuestionIds = randomQuestions.Select(q => q.QuestionId).ToList();
                TempData["SelectedQuestionIds"] = JsonSerializer.Serialize(selectedQuestionIds);
                
                // Store the questions in their original order with their IDs for exact matching later
                var orderedQuestions = randomQuestions.Select((q, index) => new {
                    QuestionId = q.QuestionId,
                    Order = index
                }).ToDictionary(x => x.QuestionId, x => x.Order);
                TempData["QuestionOrder"] = JsonSerializer.Serialize(orderedQuestions);

                _logger.LogInformation($"Found test for taking: {test.Title} with ID: {test.TestIdentifier}");
                return View(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving test for taking: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        // GET: /Test/Result
        [HttpGet]
        public async Task<IActionResult> Result(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var test = await _testRepository.GetByStringIdAsync(id);
                if (test == null)
                {
                    return NotFound();
                }

                // Pass along any info messages to the view
                if (TempData.ContainsKey("InfoMessage"))
                {
                    ViewBag.InfoMessage = TempData["InfoMessage"];
                }

                // For GET requests, we'll just render the view with test data
                // The actual results will be populated by JavaScript
                return View(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving test for result: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        // POST: /Test/SubmitTest
        [HttpPost]
        public async Task<IActionResult> SubmitTest([FromBody] TestSubmissionModel submission)
        {
            if (submission == null)
            {
                return BadRequest();
            }

            try
            {
                var test = await _testRepository.GetByStringIdAsync(submission.TestId);
                if (test == null)
                {
                    return NotFound();
                }

                // Get Vietnam time for submission
                DateTime vietnamTime = GetVietnamTime();

                // Store the submission data in TempData so it's available across requests
                TempData["UserAnswers"] = JsonSerializer.Serialize(submission.UserAnswers);
                TempData["Score"] = submission.Score;
                TempData["CorrectCount"] = submission.CorrectCount;
                TempData["TimeTaken"] = submission.TimeTaken;
                TempData["TimeUsed"] = submission.TimeUsed;
                TempData["CompletedAt"] = vietnamTime; // Store completion time

                // Keep both selected question IDs and their order
                if (TempData.ContainsKey("SelectedQuestionIds"))
                {
                    TempData.Keep("SelectedQuestionIds");
                }
                
                if (TempData.ContainsKey("QuestionOrder"))
                {
                    TempData.Keep("QuestionOrder");
                }

                // Get current user ID if authenticated
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                // Save completed test to database if user is logged in
                if (!string.IsNullOrEmpty(userId))
                {
                    await SaveCompletedTestToDatabase(userId, test, submission, vietnamTime);
                }
                
                // Always save to session as a fallback
                SaveCompletedTestToSession(test, submission, vietnamTime);

                // Return success response
                return Ok(new { redirectUrl = Url.Action("Result", "Test", new { id = submission.TestId }) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing test submission: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your submission.");
            }
        }

        // GET: /Test/Detail
        [HttpGet]
        public async Task<IActionResult> Detail(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var test = await _testRepository.GetByStringIdAsync(id);
                if (test == null)
                {
                    return NotFound();
                }

                // Check if we have COMPLETE test data in TempData
                bool hasCompleteData = TempData.ContainsKey("UserAnswers") &&
                    TempData.ContainsKey("Score") &&
                    TempData.ContainsKey("CorrectCount") &&
                    TempData.ContainsKey("SelectedQuestionIds");

                // Get current user ID
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                UserTestModel userTest = null;
                
                // Try to get the completed test from database if user is logged in
                if (!string.IsNullOrEmpty(userId))
                {
                    userTest = await _userTestRepository.GetByUserAndTestIdAsync(userId, id);
                }

                // Get completed test from session as fallback
                var sessionTest = GetCompletedTestsFromSession()?.FirstOrDefault(t => t.TestId == id);

                // Check for saved test data in session (from interrupted retake)
                string sessionKey = $"LastCompletedTest_{id}";
                Dictionary<string, string> savedTestData = null;
                if (!hasCompleteData && HttpContext.Session.GetString(sessionKey) is string savedJson)
                {
                    savedTestData = JsonSerializer.Deserialize<Dictionary<string, string>>(savedJson);
                    if (savedTestData != null && 
                        savedTestData.ContainsKey("UserAnswers") && 
                        savedTestData.ContainsKey("Score") && 
                        savedTestData.ContainsKey("CorrectCount") &&
                        savedTestData.ContainsKey("SelectedQuestionIds"))
                    {
                        // We found saved test data, restore it
                        hasCompleteData = true;
                        
                        ViewBag.UserAnswers = JsonSerializer.Deserialize<int[]>(savedTestData["UserAnswers"]);
                        ViewBag.Score = Convert.ToInt32(savedTestData["Score"]);
                        ViewBag.CorrectCount = Convert.ToInt32(savedTestData["CorrectCount"]);
                        ViewBag.TimeTaken = savedTestData["TimeTaken"];
                        
                        if (savedTestData.ContainsKey("CompletedAt") && !string.IsNullOrEmpty(savedTestData["CompletedAt"]))
                        {
                            ViewBag.CompletedAt = Convert.ToDateTime(savedTestData["CompletedAt"]);
                        }
                        
                        var selectedQuestionIds = JsonSerializer.Deserialize<List<int>>(savedTestData["SelectedQuestionIds"]);
                        
                        Dictionary<int, int> questionOrder = null;
                        if (savedTestData.ContainsKey("QuestionOrder") && !string.IsNullOrEmpty(savedTestData["QuestionOrder"]))
                        {
                            questionOrder = JsonSerializer.Deserialize<Dictionary<int, int>>(savedTestData["QuestionOrder"]);
                        }
                        
                        // Create a dictionary of all questions for easy lookup
                        var allQuestionsDict = test.Questions.ToDictionary(q => q.QuestionId);
                        
                        // Build new questions list with the exact questions in the exact order
                        var orderedQuestions = new List<TestQuestionModel>();
                        
                        foreach (var questionId in selectedQuestionIds)
                        {
                            if (allQuestionsDict.TryGetValue(questionId, out var question))
                            {
                                orderedQuestions.Add(question);
                            }
                        }
                        
                        // If we have question order information, use it to sort
                        if (questionOrder != null)
                        {
                            orderedQuestions = orderedQuestions
                                .OrderBy(q => questionOrder.ContainsKey(q.QuestionId) ? 
                                    questionOrder[q.QuestionId] : int.MaxValue)
                                .ToList();
                        }
                        
                        // Replace the questions with our ordered list
                        test.Questions = orderedQuestions;
                        
                        // Add a message to indicate we're showing saved results
                        ViewBag.InfoMessage = "Hiển thị kết quả chi tiết của lần làm bài gần nhất đã hoàn thành.";
                    }
                }

                // Use the most recent completed test data source
                if (hasCompleteData && savedTestData == null)
                {
                    // We have complete data in TempData, use it
                    ViewBag.UserAnswers = JsonSerializer.Deserialize<int[]>(TempData["UserAnswers"].ToString());
                    ViewBag.Score = TempData["Score"];
                    ViewBag.CorrectCount = TempData["CorrectCount"];
                    ViewBag.TimeTaken = TempData["TimeTaken"];
                    
                    // Add completion time to ViewBag
                    if (TempData.ContainsKey("CompletedAt"))
                    {
                        ViewBag.CompletedAt = Convert.ToDateTime(TempData["CompletedAt"]);
                    }
                    
                    // Get the selected question IDs in the exact order they were presented
                    var selectedQuestionIds = JsonSerializer.Deserialize<List<int>>(
                        TempData["SelectedQuestionIds"].ToString());
                    
                    Dictionary<int, int> questionOrder = null;
                    if (TempData.ContainsKey("QuestionOrder"))
                    {
                        questionOrder = JsonSerializer.Deserialize<Dictionary<int, int>>(
                            TempData["QuestionOrder"].ToString());
                    }
                    
                    // Create a dictionary of all questions for easy lookup
                    var allQuestionsDict = test.Questions.ToDictionary(q => q.QuestionId);
                    
                    // Build new questions list with the exact questions in the exact order
                    var orderedQuestions = new List<TestQuestionModel>();
                    
                    foreach (var questionId in selectedQuestionIds)
                    {
                        if (allQuestionsDict.TryGetValue(questionId, out var question))
                        {
                            orderedQuestions.Add(question);
                        }
                    }
                    
                    // If we have question order information, use it to sort
                    if (questionOrder != null)
                    {
                        orderedQuestions = orderedQuestions
                            .OrderBy(q => questionOrder.ContainsKey(q.QuestionId) ? 
                                questionOrder[q.QuestionId] : int.MaxValue)
                            .ToList();
                    }
                    
                    // Replace the questions with our ordered list
                    test.Questions = orderedQuestions;
                    
                    // Keep the data for future requests
                    TempData.Keep("UserAnswers");
                    TempData.Keep("Score");
                    TempData.Keep("CorrectCount");
                    TempData.Keep("TimeTaken");
                    TempData.Keep("CompletedAt");
                    TempData.Keep("SelectedQuestionIds");
                    TempData.Keep("QuestionOrder");
                }
                else if (!hasCompleteData && (userTest != null || sessionTest != null))
                {
                    // We have a completed test in database or session but no saved data
                    // Since we can't show the exact questions/answers from a past attempt,
                    // redirect to the Result page which shows the summary
                    TempData["InfoMessage"] = "Chi tiết câu hỏi không còn khả dụng sau khi bạn đã bắt đầu làm lại bài kiểm tra. Đây là tổng kết kết quả của bài làm trước đó.";
                    return RedirectToAction("Result", new { id = id });
                }
                else if (!hasCompleteData)
                {
                    // No test data available anywhere - redirect to Take action
                    return RedirectToAction("Take", new { id = id });
                }

                return View(test);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving test details: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        // GET: /Test/Completed
        [HttpGet]
        public async Task<IActionResult> Completed()
        {
            try
            {
                List<TestModel> testDetailsList = new List<TestModel>();
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                if (!string.IsNullOrEmpty(userId))
                {
                    // User is logged in, get tests from database
                    var completedUserTests = await _userTestRepository.GetCompletedTestsByUserIdAsync(userId);
                    
                    if (completedUserTests != null && completedUserTests.Any())
                    {
                        foreach (var userTest in completedUserTests)
                        {
                            var test = await _testRepository.GetByStringIdAsync(userTest.TestId);
                            if (test != null)
                            {
                                // Convert UserTestModel to CompletedTestModel for consistent display
                                var completionInfo = new CompletedTestModel
                                {
                                    TestId = userTest.TestId,
                                    TestTitle = userTest.TestTitle,
                                    TestCategory = userTest.TestCategory,
                                    TestLevel = userTest.TestLevel,
                                    ImageUrl = userTest.ImageUrl,
                                    Score = userTest.Score,
                                    CorrectCount = userTest.CorrectCount,
                                    TotalQuestions = userTest.TotalQuestions,
                                    TimeTaken = userTest.TimeTaken,
                                    CompletedAt = userTest.CompletedAt
                                };
                                
                                test.CompletionInfo = completionInfo;
                                testDetailsList.Add(test);
                            }
                        }
                    }
                }
                else
                {
                    // User not logged in, fall back to session
                    var sessionCompletedTests = GetCompletedTestsFromSession();
                    
                    if (sessionCompletedTests != null && sessionCompletedTests.Any())
                    {
                        foreach (var completedTest in sessionCompletedTests)
                        {
                            var test = await _testRepository.GetByStringIdAsync(completedTest.TestId);
                            if (test != null)
                            {
                                test.CompletionInfo = completedTest;
                                testDetailsList.Add(test);
                            }
                        }
                    }
                }
                
                if (!testDetailsList.Any())
                {
                    ViewBag.CompletedMessage = "Bạn chưa hoàn thành bài kiểm tra nào. Hãy thử làm một bài kiểm tra và quay lại đây sau.";
                    return View("Index", new List<TestModel>());
                }
                
                ViewBag.IsCompletedPage = true;
                return View("Index", testDetailsList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completed tests");
                TempData["Error"] = "Không thể tải danh sách bài kiểm tra đã hoàn thành. Vui lòng thử lại.";
                return View("Index", new List<TestModel>());
            }
        }

        // GET: /Test/Progress
        [HttpGet]
        public async Task<IActionResult> Progress()
        {
            try
            {
                // Get all available tests for statistics
                var allTests = await _testRepository.GetAllTestsAsync();
                int totalAvailableTests = allTests.Count;
                
                // Initialize statistics model
                var progressModel = new TestProgressModel
                {
                    TotalAvailableTests = totalAvailableTests,
                    CompletedTests = new List<CompletedTestModel>(),
                    CompletedCount = 0,
                    AverageScore = 0,
                    CategoryProgress = new Dictionary<string, CategoryProgressModel>(),
                    LevelProgress = new Dictionary<string, LevelProgressModel>()
                };
                
                // Get completed tests
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                List<CompletedTestModel> completedTests = new List<CompletedTestModel>();
                
                if (!string.IsNullOrEmpty(userId))
                {
                    // User is logged in, get tests from database
                    var userTests = await _userTestRepository.GetCompletedTestsByUserIdAsync(userId);
                    
                    if (userTests != null && userTests.Any())
                    {
                        completedTests = userTests.Select(ut => new CompletedTestModel
                        {
                            TestId = ut.TestId,
                            TestTitle = ut.TestTitle,
                            TestCategory = ut.TestCategory,
                            TestLevel = ut.TestLevel,
                            ImageUrl = ut.ImageUrl,
                            Score = ut.Score,
                            CorrectCount = ut.CorrectCount,
                            TotalQuestions = ut.TotalQuestions,
                            TimeTaken = ut.TimeTaken,
                            CompletedAt = ut.CompletedAt,
                            // Add a property for Detail URL to use in the view
                            DetailUrl = Url.Action("Detail", "Test", new { id = ut.TestId })
                        }).ToList();
                    }
                }
                else
                {
                    // User not logged in, fall back to session
                    var sessionTests = GetCompletedTestsFromSession() ?? new List<CompletedTestModel>();
                    
                    // Add Detail URLs to the session tests
                    completedTests = sessionTests.Select(test => {
                        test.DetailUrl = Url.Action("Detail", "Test", new { id = test.TestId });
                        return test;
                    }).ToList();
                }
                
                // Update progress model with completed tests
                progressModel.CompletedTests = completedTests;
                progressModel.CompletedCount = completedTests.Count;
                progressModel.CompletionPercentage = totalAvailableTests > 0 
                    ? (double)completedTests.Count / totalAvailableTests * 100 
                    : 0;
                    
                // Calculate average score
                if (completedTests.Any())
                {
                    progressModel.AverageScore = completedTests.Average(t => t.Score);
                }
                
                // Group by category
                var categoryCounts = allTests.GroupBy(t => t.Category)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var category in categoryCounts.Keys)
                {
                    var categoryCompletedTests = completedTests
                        .Where(t => t.TestCategory == category)
                        .ToList();
                    
                    progressModel.CategoryProgress[category] = new CategoryProgressModel
                    {
                        TotalTests = categoryCounts[category],
                        CompletedTests = categoryCompletedTests.Count,
                        CompletionPercentage = categoryCounts[category] > 0 
                            ? (double)categoryCompletedTests.Count / categoryCounts[category] * 100 
                            : 0,
                        AverageScore = categoryCompletedTests.Any() 
                            ? categoryCompletedTests.Average(t => t.Score) 
                            : 0
                    };
                }
                
                // Group by level
                var levelCounts = allTests.GroupBy(t => t.Level)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var level in levelCounts.Keys)
                {
                    var levelCompletedTests = completedTests
                        .Where(t => t.TestLevel == level)
                        .ToList();
                    
                    progressModel.LevelProgress[level] = new LevelProgressModel
                    {
                        TotalTests = levelCounts[level],
                        CompletedTests = levelCompletedTests.Count,
                        CompletionPercentage = levelCounts[level] > 0 
                            ? (double)levelCompletedTests.Count / levelCounts[level] * 100 
                            : 0,
                        AverageScore = levelCompletedTests.Any() 
                            ? levelCompletedTests.Average(t => t.Score) 
                            : 0
                    };
                }
                
                // Get recent test completions
                progressModel.RecentCompletions = completedTests
                    .OrderByDescending(t => t.CompletedAt)
                    .Take(5)
                    .ToList();
                
                // Add a flag to ViewBag to indicate we want to use Detail links
                ViewBag.UseDetailLinks = true;
                
                return View(progressModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving progress data");
                TempData["Error"] = "Không thể tải dữ liệu tiến trình học tập. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        // Helper method to save completed test to database
        private async Task SaveCompletedTestToDatabase(string userId, TestModel test, TestSubmissionModel submission, DateTime completionTime)
        {
            // Check if this test was already completed by the user
            var existingTest = await _userTestRepository.GetByUserAndTestIdAsync(userId, test.Id);
            
            UserTestModel userTest;
            if (existingTest != null)
            {
                // Update existing record
                userTest = existingTest;
                userTest.Score = submission.Score;
                userTest.CorrectCount = submission.CorrectCount;
                userTest.TimeTaken = submission.TimeTaken;
                userTest.CompletedAt = completionTime;
            }
            else
            {
                // Create new record
                userTest = new UserTestModel
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
                    CompletedAt = completionTime
                };
            }
            
            await _userTestRepository.SaveUserTestAsync(userTest);
        }

        // Helper method to save completed test to session
        private void SaveCompletedTestToSession(TestModel test, TestSubmissionModel submission, DateTime completionTime)
        {
            var completedTest = new CompletedTestModel
            {
                TestId = test.Id,
                TestTitle = test.Title,
                TestCategory = test.Category,
                TestLevel = test.Level,
                ImageUrl = test.ImageUrl,
                Score = submission.Score,
                CorrectCount = submission.CorrectCount,
                TotalQuestions = test.Questions.Count,
                TimeTaken = submission.TimeTaken,
                CompletedAt = completionTime
            };
            
            // Get existing completed tests
            var completedTests = GetCompletedTestsFromSession() ?? new List<CompletedTestModel>();
            
            // Remove any existing entry for this test
            completedTests.RemoveAll(t => t.TestId == test.Id);
            
            // Add the new completion
            completedTests.Add(completedTest);
            
            // Save back to session
            HttpContext.Session.SetString("CompletedTests", JsonSerializer.Serialize(completedTests));
        }

        // Helper method to get completed tests from session
        private List<CompletedTestModel> GetCompletedTestsFromSession()
        {
            var completedTestsJson = HttpContext.Session.GetString("CompletedTests");
            if (string.IsNullOrEmpty(completedTestsJson))
            {
                return new List<CompletedTestModel>();
            }
            
            return JsonSerializer.Deserialize<List<CompletedTestModel>>(completedTestsJson);
        }

        // Helper method to get Vietnam time
        private DateTime GetVietnamTime()
        {
            try
            {
                TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTime(DateTime.Now, vietnamZone);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback if the timezone isn't found (e.g., on Linux/macOS)
                return DateTime.Now.AddHours(7); // GMT+7
            }
            catch (Exception)
            {
                // If any other error occurs, return the server time
                return DateTime.Now;
            }
        }
    }
}