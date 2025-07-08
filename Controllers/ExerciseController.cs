using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TiengAnh.Models;
using TiengAnh.Repositories;
using TiengAnh.Services;

namespace TiengAnh.Controllers
{
    public class ExerciseController : Controller
    {
        private readonly ExerciseRepository _exerciseRepository;
        private readonly TopicRepository _topicRepository;
        private readonly ILogger<ExerciseController> _logger;

        public ExerciseController(
            ExerciseRepository exerciseRepository, 
            TopicRepository topicRepository,
            ILogger<ExerciseController> logger)
        {
            _exerciseRepository = exerciseRepository;
            _topicRepository = topicRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var topics = await _topicRepository.GetAllTopicsAsync();
                
                // For each topic, get the exercises count
                foreach (var topic in topics)
                {
                    var exercises = await _exerciseRepository.GetExercisesByTopicIdAsync(topic.ID_CD);
                    topic.ExerciseCount = exercises.Count;
                    
                    // Count by exercise type
                    topic.MultipleChoiceCount = exercises.Count(e => e.ExerciseType == "MultipleChoice");
                    topic.FillBlankCount = exercises.Count(e => e.ExerciseType == "FillBlank");
                    topic.WordOrderingCount = exercises.Count(e => e.ExerciseType == "WordOrdering");
                    
                    _logger.LogInformation($"Topic {topic.ID_CD} ({topic.Name_CD}): {topic.ExerciseCount} exercises " +
                        $"(MC: {topic.MultipleChoiceCount}, FB: {topic.FillBlankCount}, WO: {topic.WordOrderingCount})");
                }
                
                return View(topics);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving topics and exercises for Index view");
                return View(new List<TopicModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Topic(int id)
        {
            // Get topic information
            var topic = await _topicRepository.GetTopicByIdAsync(id);
            if (topic == null)
            {
                return NotFound();
            }

            // Get exercises for this topic
            var exercises = await _exerciseRepository.GetExercisesByTopicIdAsync(id);
            
            ViewBag.Topic = topic;
            
            return View(exercises);
        }

        [HttpGet]
        public async Task<IActionResult> MultipleChoice(int id)
        {
            var exercise = await _exerciseRepository.GetByExerciseIdAsync(id);
            if (exercise == null)
            {
                return NotFound();
            }
            
            // Get topic information for breadcrumbs
            var topic = await _topicRepository.GetTopicByIdAsync(exercise.ID_CD);
            ViewBag.Topic = topic;
            
            // Get all exercises for this topic to determine progress
            var allTopicExercises = await _exerciseRepository.GetExercisesByTopicIdAsync(exercise.ID_CD);
            
            // Filter to get only multiple choice exercises if needed
            var relevantExercises = allTopicExercises
                .Where(e => e.ExerciseType == exercise.ExerciseType)
                .OrderBy(e => e.ID_BT)
                .ToList();
            
            // Find the index (0-based) of the current exercise
            int currentIndex = relevantExercises.FindIndex(e => e.ID_BT == id);
            
            // Set progress information in ViewBag
            ViewBag.CurrentQuestionNumber = currentIndex + 1; // Convert to 1-based index for display
            ViewBag.TotalQuestions = relevantExercises.Count;
            
            // If there are next/previous exercises, add their IDs to ViewBag for navigation
            if (currentIndex > 0)
            {
                ViewBag.PreviousExerciseId = relevantExercises[currentIndex - 1].ID_BT;
            }
            
            if (currentIndex < relevantExercises.Count - 1)
            {
                ViewBag.NextExerciseId = relevantExercises[currentIndex + 1].ID_BT;
            }
            else
            {
                // If this is the last exercise, add the topic ID to ViewBag for results navigation
                ViewBag.IsLastExercise = true;
                ViewBag.TopicId = topic.ID_CD;
            }
            
            return View(exercise);
        }

        // New method to display results after completing exercises
        [HttpGet]
        public async Task<IActionResult> Result(int topicId, int correctAnswers, int totalQuestions)
        {
            var topic = await _topicRepository.GetTopicByIdAsync(topicId);
            if (topic == null)
            {
                return NotFound();
            }
            
            var resultModel = new ExerciseResultModel
            {
                TopicId = topicId,
                TopicName = topic.Name_CD,
                CorrectAnswers = correctAnswers,
                TotalQuestions = totalQuestions,
                CompletionDate = DateTime.Now,
                ExerciseType = "MultipleChoice"
            };
            
            return View(resultModel);
        }

        // Phương thức xử lý bài tập điền từ
        [HttpGet]
        public async Task<IActionResult> FillBlank(int id)
        {
            var topic = await _topicRepository.GetTopicByIdAsync(id);
            if (topic == null)
            {
                return NotFound();
            }

            // Lấy các bài tập điền từ từ cơ sở dữ liệu
            var exercises = await _exerciseRepository.GetExercisesByTopicIdAsync(id);
            // Lọc theo loại bài tập (nếu có)
            var fillBlankExercises = exercises.Where(e => e.ExerciseType == "FillBlank").ToList();
            
            if (fillBlankExercises.Count == 0)
            {
                // Nếu không có bài tập loại này, sử dụng tất cả bài tập của chủ đề
                fillBlankExercises = exercises;
            }
            
            ViewBag.Topic = topic;
            return View(fillBlankExercises);
        }

        // Phương thức xử lý bài tập sắp xếp câu
        [HttpGet]
        public async Task<IActionResult> WordOrdering(int id)
        {
            var topic = await _topicRepository.GetTopicByIdAsync(id);
            if (topic == null)
            {
                return NotFound();
            }

            // Lấy các bài tập từ cơ sở dữ liệu
            var exercises = await _exerciseRepository.GetExercisesByTopicIdAsync(id);
            // Lọc theo loại bài tập (nếu có)
            var wordOrderingExercises = exercises.Where(e => e.ExerciseType == "WordOrdering").ToList();
            
            if (wordOrderingExercises.Count == 0)
            {
                // Nếu không có bài tập loại này, sử dụng tất cả bài tập của chủ đề
                wordOrderingExercises = exercises;
            }
            
            ViewBag.Topic = topic;
            return View(wordOrderingExercises);
        }

        [HttpPost]
        public async Task<IActionResult> CheckAnswer(int exerciseId, string selectedAnswer)
        {
            try
            {
                var exercise = await _exerciseRepository.GetByExerciseIdAsync(exerciseId);
                if (exercise == null)
                {
                    return Json(new { success = false, message = "Exercise not found" });
                }
                
                // Normalize inputs
                string correctAnswer = exercise.Answer_BT?.Trim() ?? "";
                selectedAnswer = selectedAnswer?.Trim() ?? "";
                
                bool isCorrect = false;
                
                // Check if selectedAnswer matches correctAnswer directly (case insensitive)
                if (string.Equals(selectedAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    isCorrect = true;
                }
                // Check if correctAnswer is one of A, B, C, D and matches the selectedAnswer
                else if (new[] { "A", "B", "C", "D" }.Contains(correctAnswer, StringComparer.OrdinalIgnoreCase) && 
                        string.Equals(correctAnswer, selectedAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    isCorrect = true;
                }
                // Check if selectedAnswer is A, B, C, D and matches the corresponding option content
                else if (selectedAnswer.ToUpper() == "A" && string.Equals(exercise.Option_A, correctAnswer, StringComparison.OrdinalIgnoreCase) ||
                        selectedAnswer.ToUpper() == "B" && string.Equals(exercise.Option_B, correctAnswer, StringComparison.OrdinalIgnoreCase) ||
                        selectedAnswer.ToUpper() == "C" && string.Equals(exercise.Option_C, correctAnswer, StringComparison.OrdinalIgnoreCase) ||
                        selectedAnswer.ToUpper() == "D" && string.Equals(exercise.Option_D, correctAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    isCorrect = true;
                }
                
                return Json(new
                {
                    success = true,
                    isCorrect = isCorrect,
                    correctAnswer = correctAnswer,
                    explanation = exercise.Explanation_BT
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking answer");
                return Json(new { success = false, message = "Error checking answer" });
            }
        }
    }
}
