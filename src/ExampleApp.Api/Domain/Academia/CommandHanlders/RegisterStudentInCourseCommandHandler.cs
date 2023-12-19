using ExampleApp.Api.Domain.Academia.Commands;
using ExampleApp.Api.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExampleApp.Api.Domain.Academia.CommandHanlders;

internal class RegisterStudentInCourseCommandHandler : IRequestHandler<RegisterStudentInCourse, bool>
{
    private readonly AcademiaDbContext _context;
    private readonly ILogger<RegisterStudentInCourseCommandHandler> _logger;
    private DateOnly today = new(2023, 12, 1); //TODO: in order to have current courses.

    public RegisterStudentInCourseCommandHandler(
        AcademiaDbContext context,
        ILogger<RegisterStudentInCourseCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> Handle(RegisterStudentInCourse request, CancellationToken cancellationToken)
    {
        // Retrieve the student and course from the database
        var student = await GetStudent(request.FullName, request.Badge);

        if (student == null)
        {
            _logger.LogWarning("Student not found. Registration failed.");
            return false;
        }

        var course = await _context.Courses
                    .Include(c => c.Semester) 
                    .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken: cancellationToken);

        if (student == null || course == null)
        {
            _logger.LogWarning("Student or course not found. Registration failed.");
            return false;
        }

        // Check if the student is registered in the course
        var existingRegistration = await _context.StudentCourses
            .FirstOrDefaultAsync(sc => sc.StudentId == student.Id && sc.CourseId == request.CourseId);

        if (existingRegistration != null)
        {
            _logger.LogInformation("Student {StudentId} is already registered in course {CourseId}", student.Id, request.CourseId);
            return false;
        }

        // Check for current course
        if (!IsCourseCurrent(course, request))
        {
            _logger.LogWarning("Student cannot register for a past or future course. Registration failed.");
            return false;
        }

        var studentCourse = new StudentCourse
        {
            Student = student,
            Course = course
        };

        _context.StudentCourses.Add(studentCourse);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Student {StudentId} registered in course {CourseId}", student.Id, request.CourseId);
        return true;
    }

    private async Task<Student> GetStudent(string fullName, string badge)
    {
        if (!string.IsNullOrEmpty(fullName))
        {
            return await _context.Students.FirstOrDefaultAsync(s => s.FullName == fullName);
        }
        else if (!string.IsNullOrEmpty(badge))
        {
            return await _context.Students.FirstOrDefaultAsync(s => s.Badge == badge);
        }

        return null;
    }

    private bool IsCourseCurrent(Course course, RegisterStudentInCourse request)
    {
        return course != null &&
           course.Semester != null &&
           course.Semester.Start <= today &&
           today <= course.Semester.End;
    }
}