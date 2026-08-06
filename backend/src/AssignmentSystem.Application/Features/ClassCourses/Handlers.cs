using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.ClassCourses;

public sealed class CreateClassCourseHandler : ICommandHandler<CreateClassCourseCommand, ClassCourseDto>
{
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly ClassCourseMapper Mapper = new();

    public CreateClassCourseHandler(
        IRepository<ClassCourse> classCourseRepository,
        IRepository<Class> classRepository,
        IRepository<Course> courseRepository,
        IUnitOfWork unitOfWork)
    {
        _classCourseRepository = classCourseRepository;
        _classRepository = classRepository;
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassCourseDto>> HandleAsync(CreateClassCourseCommand command, CancellationToken ct = default)
    {
        var classObj = await _classRepository.GetByIdAsync(command.ClassId, ct);
        if (classObj is null)
        {
            return Result<ClassCourseDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        var course = await _courseRepository.GetByIdAsync(command.CourseId, ct);
        if (course is null)
        {
            return Result<ClassCourseDto>.Failure(Error.NotFound("Course.NotFound", "The specified course was not found."));
        }

        // Checked here as well as by the unique index: a 409 with a sentence beats a
        // constraint violation surfacing as a 500.
        var duplicateSpec = new ClassCourseDuplicateSpecification(command.ClassId, command.CourseId);
        if (await _classCourseRepository.AnyAsync(duplicateSpec, ct))
        {
            return Result<ClassCourseDto>.Failure(Error.Conflict(
                "ClassCourse.Duplicate", "This class already offers this course."));
        }

        try
        {
            var classCourse = ClassCourse.Create(command.ClassId, command.CourseId);
            await _classCourseRepository.AddAsync(classCourse, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var fetchSpec = new ClassCourseWithDetailsSpecification(classCourse.Id);
            var saved = await _classCourseRepository.FirstOrDefaultAsync(fetchSpec, ct);

            if (saved is null)
            {
                // Unreachable — see the note on CreateAssignmentHandler. The DTO needs the
                // class and course names, which live behind navigations.
                return Result<ClassCourseDto>.Failure(Error.Failure(
                    "ClassCourse.NotReloaded", "The offering was created but could not be read back."));
            }

            return Mapper.MapToDto(saved);
        }
        catch (DomainException ex)
        {
            return Result<ClassCourseDto>.Failure(Error.Validation("ClassCourse.Invalid", ex.Message));
        }
    }
}

/// <summary>
/// Removes an offering. Refuses while anything still hangs off it — a teaching mapping
/// or an assignment — rather than cascading: dropping an offering must not silently take
/// student work with it. The admin unwinds it deliberately, in order.
/// </summary>
public sealed class DeleteClassCourseHandler : ICommandHandler<DeleteClassCourseCommand>
{
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteClassCourseHandler(
        IRepository<ClassCourse> classCourseRepository,
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        IRepository<Assignment> assignmentRepository,
        IUnitOfWork unitOfWork)
    {
        _classCourseRepository = classCourseRepository;
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteClassCourseCommand command, CancellationToken ct = default)
    {
        var classCourse = await _classCourseRepository.GetByIdAsync(command.Id, ct);
        if (classCourse is null)
        {
            return Result.Failure(Error.NotFound("ClassCourse.NotFound", "The specified course offering was not found."));
        }

        var assignmentSpec = new AssignmentsByClassCourseSpecification(command.Id);
        if (await _assignmentRepository.AnyAsync(assignmentSpec, ct))
        {
            return Result.Failure(Error.Conflict(
                "ClassCourse.InUse",
                "This offering has assignments. Delete them before removing the offering."));
        }

        var teachingSpec = new TeacherAssignmentsByClassCourseSpecification(command.Id);
        if (await _teacherAssignmentRepository.AnyAsync(teachingSpec, ct))
        {
            return Result.Failure(Error.Conflict(
                "ClassCourse.InUse",
                "Teachers are assigned to this offering. Remove those mappings before removing the offering."));
        }

        _classCourseRepository.Remove(classCourse);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetClassCourseByIdHandler : IQueryHandler<GetClassCourseByIdQuery, ClassCourseDto>
{
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly IClassCourseUsageReader _usageReader;
    private static readonly ClassCourseMapper Mapper = new();

    public GetClassCourseByIdHandler(
        IRepository<ClassCourse> classCourseRepository,
        IClassCourseUsageReader usageReader)
    {
        _classCourseRepository = classCourseRepository;
        _usageReader = usageReader;
    }

    public async Task<Result<ClassCourseDto>> HandleAsync(GetClassCourseByIdQuery query, CancellationToken ct = default)
    {
        var spec = new ClassCourseWithDetailsSpecification(query.Id);
        var classCourse = await _classCourseRepository.FirstOrDefaultAsync(spec, ct);
        if (classCourse is null)
        {
            return Result<ClassCourseDto>.Failure(Error.NotFound("ClassCourse.NotFound", "The specified course offering was not found."));
        }

        var usage = await _usageReader.GetUsageAsync([classCourse.Id], ct);
        var counts = usage.GetValueOrDefault(classCourse.Id);

        return Mapper.MapToDto(classCourse) with
        {
            TeacherCount = counts.TeacherCount,
            AssignmentCount = counts.AssignmentCount,
        };
    }
}

public sealed class GetClassCoursesHandler : IQueryHandler<GetClassCoursesQuery, PageResult<ClassCourseDto>>
{
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly IClassCourseUsageReader _usageReader;
    private static readonly ClassCourseMapper Mapper = new();

    public GetClassCoursesHandler(
        IRepository<ClassCourse> classCourseRepository,
        IClassCourseUsageReader usageReader)
    {
        _classCourseRepository = classCourseRepository;
        _usageReader = usageReader;
    }

    public async Task<Result<PageResult<ClassCourseDto>>> HandleAsync(GetClassCoursesQuery query, CancellationToken ct = default)
    {
        var spec = new ClassCoursesPagedSpecification(query.ClassId, query.CourseId, query.Search, query.SortBy, query.SortDir, query.Page, query.PageSize);
        var paged = await _classCourseRepository.ListPagedAsync(spec, ct);

        // Two grouped queries for the whole page rather than two per row.
        var usage = await _usageReader.GetUsageAsync(paged.Items.Select(cc => cc.Id).ToList(), ct);

        var items = paged.Items
            .Select(cc =>
            {
                var counts = usage.GetValueOrDefault(cc.Id);
                return Mapper.MapToDto(cc) with
                {
                    TeacherCount = counts.TeacherCount,
                    AssignmentCount = counts.AssignmentCount,
                };
            })
            .ToList();

        return new PageResult<ClassCourseDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}

internal sealed class AssignmentsByClassCourseSpecification : Specification<Assignment>
{
    public AssignmentsByClassCourseSpecification(Guid classCourseId)
    {
        Criteria = a => a.ClassCourseId == classCourseId;
    }
}

internal sealed class TeacherAssignmentsByClassCourseSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsByClassCourseSpecification(Guid classCourseId)
    {
        Criteria = ta => ta.ClassCourseId == classCourseId;
    }
}
