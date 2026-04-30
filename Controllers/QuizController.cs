using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

[Authorize]
public class QuizController : Controller
{
    private readonly QuizRepository _quizzes;
    private readonly LessonRepository _lessons;
    private readonly CourseRepository _courses;
    private readonly EnrollmentRepository _enrollments;
    private readonly UserRepository _users;
    private readonly EmailService _email;
    private readonly SettingsRepository _settings;
    private readonly ILogger<QuizController> _logger;

    public QuizController(
        QuizRepository quizzes,
        LessonRepository lessons,
        CourseRepository courses,
        EnrollmentRepository enrollments,
        UserRepository users,
        EmailService email,
        SettingsRepository settings,
        ILogger<QuizController> logger)
    {
        _quizzes = quizzes;
        _lessons = lessons;
        _courses = courses;
        _enrollments = enrollments;
        _users = users;
        _email = email;
        _settings = settings;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    private async Task<bool> IsOwnerOrAdminOfCourseAsync(int courseId)
    {
        if (CurrentRole == "Admin") return true;
        var course = await _courses.GetByIdAsync(courseId);
        return course != null && course.TeacherId == CurrentUserId;
    }

    private async Task<(Quiz? quiz, Lesson? lesson, int courseId)> GetQuizContextAsync(int quizId)
    {
        var quiz = await _quizzes.GetByIdAsync(quizId);
        if (quiz == null) return (null, null, 0);
        var lesson = await _lessons.GetByIdAsync(quiz.LessonId);
        return (quiz, lesson, lesson?.CourseId ?? 0);
    }

    private async Task<IActionResult?> RequireQuizAccessAsync(int quizId)
    {
        var (quiz, lesson, courseId) = await GetQuizContextAsync(quizId);
        if (quiz == null || lesson == null) return NotFound();

        if (CurrentRole == "Admin") return null;

        if (User.IsInRole("CanTeach"))
        {
            if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();
            return null;
        }

        if (!lesson.IsPublished) return Forbid();
        if (!await _enrollments.IsEnrolledAsync(CurrentUserId, courseId)) return Forbid();
        return null;
    }

    public async Task<IActionResult> Take(int id)
    {
        var access = await RequireQuizAccessAsync(id);
        if (access != null) return access;

        var quiz = await _quizzes.GetByIdAsync(id, withQuestions: true);
        if (quiz == null) return NotFound();
        return View(quiz);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, Dictionary<int, int> answers)
    {
        var access = await RequireQuizAccessAsync(id);
        if (access != null) return access;

        var attempt = await _quizzes.SubmitAttemptAsync(id, CurrentUserId, answers);

        int capturedQuizId = id;
        int capturedAttemptId = attempt.Id;
        _ = NotifyQuizCompletedAsync(capturedQuizId, attempt)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception,
                        "Unhandled failure in NotifyTeacherOfQuizResultAsync for quiz {QuizId}, attempt {AttemptId}.",
                        capturedQuizId, capturedAttemptId);
            }, TaskScheduler.Default);

        return RedirectToAction("Result", new { attemptId = attempt.Id });
    }

    private async Task NotifyQuizCompletedAsync(int quizId, QuizAttempt attempt)
    {
        try
        {
            if ((await _settings.GetAsync("Notifications:CoursesEnabled")) != "true") return;

            var notifyTeacher  = (await _settings.GetAsync("Notifications:TeacherOnQuizCompleted")) == "true";
            var notifyStudent  = (await _settings.GetAsync("Notifications:StudentOnQuizCompleted")) == "true";
            if (!notifyTeacher && !notifyStudent) return;

            var (quiz, lesson, courseId) = await GetQuizContextAsync(quizId);
            if (quiz == null || courseId == 0) return;

            var course = await _courses.GetByIdAsync(courseId);
            if (course == null) return;

            var student = await _users.GetByIdAsync(attempt.UserId);
            if (student == null) return;

            if (notifyTeacher)
            {
                var teacher = await _users.GetByIdAsync(course.TeacherId);
                if (teacher != null)
                {
                    await _email.SendQuizResultToTeacherAsync(
                        teacher.Email, teacher.FullName,
                        student.FullName, student.Email,
                        attempt.QuizTitle, course.Title,
                        attempt.Score, attempt.Passed);
                    _logger.LogInformation(
                        "Quiz result notification sent: student {StudentId} scored {Score}% ({Passed}) on quiz {QuizId}, notified teacher {TeacherId}.",
                        attempt.UserId, attempt.Score, attempt.Passed ? "passed" : "failed", quizId, teacher.Id);
                }
            }

            if (notifyStudent)
            {
                await _email.SendQuizResultToStudentAsync(
                    student.Email, student.FullName,
                    attempt.QuizTitle, course.Title,
                    attempt.Score, attempt.Passed);
                _logger.LogInformation(
                    "Quiz result notification sent to student {StudentId} for quiz {QuizId}.",
                    attempt.UserId, quizId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send quiz notifications. QuizId={QuizId}, AttemptId={AttemptId}.",
                quizId, attempt.Id);
        }
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
        var access = await RequireQuizAccessAsync(quizId);
        if (access != null) return access;

        var quiz = await _quizzes.GetByIdAsync(quizId);
        if (quiz == null) return NotFound();
        var attempts = await _quizzes.GetAttemptsAsync(CurrentUserId, quizId);
        ViewBag.Quiz = quiz;
        return View(attempts);
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create(int lessonId)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(lesson.CourseId)) return Forbid();
        ViewBag.LessonTitle = lesson.Title;
        return View(new QuizFormViewModel { LessonId = lessonId });
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuizFormViewModel model, List<string> questionTexts,
        List<string> opt1, List<string> opt2, List<string> opt3, List<string> opt4, List<int> correctOpt)
    {
        if (!ModelState.IsValid) return View(model);
        var lesson = await _lessons.GetByIdAsync(model.LessonId);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(lesson.CourseId)) return Forbid();

        if (questionTexts.Count == 0 || questionTexts.All(string.IsNullOrWhiteSpace))
        {
            ModelState.AddModelError("", "Il quiz deve avere almeno una domanda.");
            return View(model);
        }

        var quiz = new Quiz
        {
            LessonId = model.LessonId,
            Title = model.Title,
            Description = model.Description,
            TimeLimitMinutes = model.TimeLimitMinutes,
            PassingScore = model.PassingScore,
            CreatedBy = CurrentUserId
        };
        var quizId = await _quizzes.CreateAsync(quiz);
        for (int i = 0; i < questionTexts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(questionTexts[i])) continue;
            int correct = correctOpt.Count > i ? correctOpt[i] : 1;
            if (correct < 1 || correct > 4) correct = 1;
            var q = new QuizQuestion
            {
                QuizId = quizId,
                QuestionText = questionTexts[i],
                SortOrder = i + 1,
                Options = new List<QuizOption>
                {
                    new() { OptionText = opt1.ElementAtOrDefault(i) ?? "", IsCorrect = correct == 1, SortOrder = 1 },
                    new() { OptionText = opt2.ElementAtOrDefault(i) ?? "", IsCorrect = correct == 2, SortOrder = 2 },
                    new() { OptionText = opt3.ElementAtOrDefault(i) ?? "", IsCorrect = correct == 3, SortOrder = 3 },
                    new() { OptionText = opt4.ElementAtOrDefault(i) ?? "", IsCorrect = correct == 4, SortOrder = 4 }
                }
            };
            await _quizzes.AddQuestionAsync(q);
        }
        TempData["Success"] = "Quiz creato!";
        return RedirectToAction("Details", "Lesson", new { id = model.LessonId });
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (quiz, lesson, courseId) = await GetQuizContextAsync(id);
        if (quiz == null) return NotFound();
        if (!await IsOwnerOrAdminOfCourseAsync(courseId)) return Forbid();
        await _quizzes.DeleteQuizAsync(id);
        TempData["Success"] = "Quiz eliminato.";
        return RedirectToAction("Details", "Lesson", new { id = quiz.LessonId });
    }
}
