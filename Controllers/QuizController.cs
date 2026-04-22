using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize]
public class QuizController : Controller
{
    private readonly QuizRepository _quizzes;
    private readonly LessonRepository _lessons;

    public QuizController(QuizRepository quizzes, LessonRepository lessons)
    {
        _quizzes = quizzes;
        _lessons = lessons;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public async Task<IActionResult> Take(int id)
    {
        var quiz = await _quizzes.GetByIdAsync(id, withQuestions: true);
        if (quiz == null) return NotFound();
        return View(quiz);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, Dictionary<int, int> answers)
    {
        var attempt = await _quizzes.SubmitAttemptAsync(id, CurrentUserId, answers);
        return RedirectToAction("Result", new { attemptId = attempt.Id });
    }

    public async Task<IActionResult> Result(int attemptId)
    {
        var attempts = await _quizzes.GetAttemptsAsync(CurrentUserId);
        var attempt = attempts.FirstOrDefault(a => a.Id == attemptId);
        if (attempt == null) return NotFound();
        return View(attempt);
    }

    public async Task<IActionResult> History(int quizId)
    {
        var quiz = await _quizzes.GetByIdAsync(quizId);
        if (quiz == null) return NotFound();
        var attempts = await _quizzes.GetAttemptsAsync(CurrentUserId, quizId);
        ViewBag.Quiz = quiz;
        return View(attempts);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create(int lessonId)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId);
        if (lesson == null) return NotFound();
        ViewBag.LessonTitle = lesson.Title;
        return View(new QuizFormViewModel { LessonId = lessonId });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuizFormViewModel model, List<string> questionTexts,
        List<string> opt1, List<string> opt2, List<string> opt3, List<string> opt4, List<int> correctOpt)
    {
        if (!ModelState.IsValid) return View(model);
        var quiz = new Quiz
        {
            LessonId = model.LessonId,
            Title = model.Title,
            Description = model.Description,
            TimeLimitMinutes = model.TimeLimitMinutes,
            PassingScore = model.PassingScore
        };
        var quizId = await _quizzes.CreateAsync(quiz);
        for (int i = 0; i < questionTexts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(questionTexts[i])) continue;
            var q = new QuizQuestion
            {
                QuizId = quizId,
                QuestionText = questionTexts[i],
                SortOrder = i + 1,
                Options = new List<QuizOption>
                {
                    new() { OptionText = opt1.ElementAtOrDefault(i) ?? "", IsCorrect = correctOpt.ElementAtOrDefault(i) == 1, SortOrder = 1 },
                    new() { OptionText = opt2.ElementAtOrDefault(i) ?? "", IsCorrect = correctOpt.ElementAtOrDefault(i) == 2, SortOrder = 2 },
                    new() { OptionText = opt3.ElementAtOrDefault(i) ?? "", IsCorrect = correctOpt.ElementAtOrDefault(i) == 3, SortOrder = 3 },
                    new() { OptionText = opt4.ElementAtOrDefault(i) ?? "", IsCorrect = correctOpt.ElementAtOrDefault(i) == 4, SortOrder = 4 }
                }
            };
            await _quizzes.AddQuestionAsync(q);
        }
        TempData["Success"] = "Quiz creato!";
        return RedirectToAction("Details", "Lesson", new { id = model.LessonId });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var quiz = await _quizzes.GetByIdAsync(id);
        if (quiz == null) return NotFound();
        var lessonId = quiz.LessonId;
        await _quizzes.DeleteQuizAsync(id);
        TempData["Success"] = "Quiz eliminato.";
        return RedirectToAction("Details", "Lesson", new { id = lessonId });
    }
}
