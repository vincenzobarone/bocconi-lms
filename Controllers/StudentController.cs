using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize(Roles = "CanAttend")]
public class StudentController : Controller
{
    private readonly EnrollmentRepository _enrollments;
    private readonly QuizRepository _quizzes;

    public StudentController(EnrollmentRepository enrollments, QuizRepository quizzes)
    {
        _enrollments = enrollments;
        _quizzes = quizzes;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public async Task<IActionResult> Dashboard()
    {
        var enrollments = await _enrollments.GetByUserAsync(CurrentUserId);
        var attempts = await _quizzes.GetAttemptsAsync(CurrentUserId);
        var vm = new StudentDashboard
        {
            Enrollments = enrollments,
            RecentAttempts = attempts.Take(5).ToList(),
            TotalCompleted = enrollments.Count(e => e.ProgressPercent == 100)
        };
        return View(vm);
    }
}
