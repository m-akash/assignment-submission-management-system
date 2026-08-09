using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
// CurrentAcademicYearSpecification: reused rather than redeclared so "the school's current
// session" has one definition — creating a student needs it to default their enrollment.
using AssignmentSystem.Application.Features.AcademicYears;
using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;
using AssignmentSystem.Application.Features.Auth;

namespace AssignmentSystem.Application.Features.Users;

/// <summary>
/// Creates a user, and for a student their first enrollment in the same transaction —
/// the domain cannot enforce "a student has a class" from inside the entity now that
/// membership is a separate row, so this handler is the choke point that guarantees it.
///
/// That first enrollment queues the same notification as a later one does: from the
/// student's side "you are in grade 9, section A" is one event, and mailing it only when an admin
/// adds them to a *second* class would be an arbitrary gap.
///
/// It also issues a single-use password-setup link and mails it. The admin still types an
/// initial password — the API contract requires one and it stays a working fallback — but
/// nothing ever transmits it, so the link is how the account actually reaches its owner.
/// A new student therefore receives two mails: one that their account exists, and one that
/// they are in a class. Two events, two messages; collapsing them would make the enrollment
/// mail conditional on how the enrollment happened.
/// </summary>
public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IClassRosterRepository _classRosterRepository;
    private readonly ITeacherRosterRepository _teacherRosterRepository;
    private readonly INotificationOutbox _notifications;
    private readonly IPasswordSetupTokenService _passwordSetup;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly UserMapper Mapper = new();

    public CreateUserHandler(
        IRepository<ApplicationUser> userRepository,
        IRepository<Class> classRepository,
        IRepository<AcademicYear> academicYearRepository,
        IRepository<StudentEnrollment> enrollmentRepository,
        IClassRosterRepository classRosterRepository,
        ITeacherRosterRepository teacherRosterRepository,
        INotificationOutbox notifications,
        IPasswordSetupTokenService passwordSetup,
        IPasswordHasher passwordHasher,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _classRepository = classRepository;
        _academicYearRepository = academicYearRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRosterRepository = classRosterRepository;
        _teacherRosterRepository = teacherRosterRepository;
        _notifications = notifications;
        _passwordSetup = passwordSetup;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> HandleAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var emailSpec = new UserByEmailSpecification(command.Email);
        var emailExists = await _userRepository.AnyAsync(emailSpec, ct);
        if (emailExists)
        {
            return Result<UserDto>.Failure(Error.Conflict("User.EmailAlreadyTaken", "A user with this email already exists."));
        }

        // The student id is derived from the class they start in, so the class has to be
        // resolved before the user is built even though the enrollment is written after it.
        string? studentId = null;
        if (command.ClassId.HasValue)
        {
            var classObj = await _classRepository.GetByIdAsync(command.ClassId.Value, ct);
            if (classObj is null)
            {
                return Result<UserDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
            }

            if (command.Role == Role.Student)
            {
                var prefix = StudentIdPrefix(classObj);
                var sequence = await _classRosterRepository.GetNextStudentSequenceAsync(prefix, ct);
                studentId = $"{prefix}-{sequence:D3}";
            }
        }

        // Resolved here rather than at the enrollment below so a missing or unknown year is
        // reported before anything has been built — the same reason the class is.
        Guid? academicYearId = null;
        if (command.Role == Role.Student && command.ClassId.HasValue)
        {
            var yearResult = await ResolveAcademicYearIdAsync(command.AcademicYearId, ct);
            if (yearResult.IsFailure)
            {
                return Result<UserDto>.Failure(yearResult.Error);
            }

            academicYearId = yearResult.Value;
        }

        string? teacherId = null;
        if (command.Role == Role.Teacher)
        {
            var sequence = await _teacherRosterRepository.GetNextTeacherSequenceAsync(ct);
            teacherId = $"INS-{sequence:D3}";
        }

        var passwordHash = _passwordHasher.Hash(command.Password);

        try
        {
            var user = ApplicationUser.Create(
                command.Email,
                command.FullName,
                passwordHash,
                command.Role,
                studentId,
                teacherId);

            await _userRepository.AddAsync(user, ct);

            // The setup link and the mail carrying it are queued here, not after the save:
            // an account that exists without a way for its owner to get into it is the one
            // outcome this handler must not be able to produce.
            var setup = await _passwordSetup.IssuePasswordSetupAsync(user.Id, ct);
            await _notifications.QueueAccountCreatedAsync(user, setup, ct);

            if (command.Role == Role.Student && command.ClassId is { } classId && academicYearId is { } yearId)
            {
                var enrollment = StudentEnrollment.Create(user.Id, classId, yearId, _clock.UtcNow);
                await _enrollmentRepository.AddAsync(enrollment, ct);
                await _notifications.QueueStudentEnrolledAsync(enrollment, ct);
            }

            // One SaveChanges for the lot: a student is never persisted classless, and no
            // account is persisted without its setup token and welcome mail beside it.
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch again with enrollments included for full DTO mapping
            var fetchSpec = new UserWithClassesByIdSpecification(user.Id);
            var savedUser = await _userRepository.FirstOrDefaultAsync(fetchSpec, ct);

            return Mapper.MapToDto(savedUser ?? user);
        }
        catch (DomainException ex)
        {
            return Result<UserDto>.Failure(Error.Validation("User.Invalid", ex.Message));
        }
    }

    /// <summary>
    /// The "9-A" part of a student id — the grade number then the section. The sequence that
    /// completes it is issued against this prefix, so the numbers are unique per grade+section.
    /// Falls back to the grade alone when no section was set, so creating a student never
    /// fails just because of that.
    /// </summary>
    private static string StudentIdPrefix(Class classObj) =>
        !string.IsNullOrWhiteSpace(classObj.Section)
            ? $"{classObj.Level}-{classObj.Section.Trim()}"
            : $"{classObj.Level}";

    /// <summary>
    /// The academic year the first enrollment belongs to: the one asked for, or the school's
    /// current session when the caller did not say. Failing outright when neither is
    /// available is the point — silently inventing a year would put the student in a session
    /// nobody chose, and the fix (create one, or flag one as current) is an admin action
    /// that the message names.
    /// </summary>
    private async Task<Result<Guid>> ResolveAcademicYearIdAsync(Guid? requestedId, CancellationToken ct)
    {
        if (requestedId is { } id)
        {
            var requested = await _academicYearRepository.GetByIdAsync(id, ct);
            return requested is null
                ? Result<Guid>.Failure(Error.NotFound(
                    "AcademicYear.NotFound", "The specified academic year was not found."))
                : Result<Guid>.Success(requested.Id);
        }

        var currentSpec = new CurrentAcademicYearSpecification();
        var current = await _academicYearRepository.FirstOrDefaultAsync(currentSpec, ct);

        return current is null
            ? Result<Guid>.Failure(Error.Validation(
                "AcademicYear.NoCurrent",
                "No academic year is set as current. Create one and mark it current, or choose a year for this student."))
            : Result<Guid>.Success(current.Id);
    }
}

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly UserMapper Mapper = new();

    public UpdateUserHandler(
        IRepository<ApplicationUser> userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        var fetchSpec = new UserWithClassesByIdSpecification(command.Id);
        var user = await _userRepository.FirstOrDefaultAsync(fetchSpec, ct);
        if (user is null)
        {
            return Result<UserDto>.Failure(Error.NotFound("User.NotFound", "The specified user was not found."));
        }

        try
        {
            user.UpdateProfile(command.FullName);

            if (!string.IsNullOrWhiteSpace(command.Password))
            {
                user.SetPasswordHash(_passwordHasher.Hash(command.Password));
            }

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch again with enrollments included for full DTO mapping
            var updatedUser = await _userRepository.FirstOrDefaultAsync(fetchSpec, ct);
            return Mapper.MapToDto(updatedUser ?? user);
        }
        catch (DomainException ex)
        {
            return Result<UserDto>.Failure(Error.Validation("User.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteUserHandler : ICommandHandler<DeleteUserCommand>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserHandler(
        IRepository<ApplicationUser> userRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(command.Id, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "The specified user was not found."));
        }

        user.SoftDelete(_clock.UtcNow);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetUserByIdHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private static readonly UserMapper Mapper = new();

    public GetUserByIdHandler(IRepository<ApplicationUser> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<UserDto>> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default)
    {
        var spec = new UserWithClassesByIdSpecification(query.Id);
        var user = await _userRepository.FirstOrDefaultAsync(spec, ct);
        if (user is null)
        {
            return Result<UserDto>.Failure(Error.NotFound("User.NotFound", "The specified user was not found."));
        }

        return Mapper.MapToDto(user);
    }
}

/// <summary>
/// Returns the caller's own profile. Backs <c>GET /api/v1/auth/me</c>, which the
/// frontend uses to rehydrate a session after a page reload — the access token lives
/// only in memory, so identity is re-fetched rather than read from browser storage.
/// </summary>
public sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly UserMapper Mapper = new();

    public GetCurrentUserHandler(IRepository<ApplicationUser> userRepository, ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<UserDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Result<UserDto>.Failure(Error.Unauthorized("Auth.NotAuthenticated", "Authentication is required."));
        }

        var spec = new UserWithClassesByIdSpecification(userId);
        var user = await _userRepository.FirstOrDefaultAsync(spec, ct);

        // The token is valid but the account is gone or deactivated — treat as unauthenticated
        // so the client clears its session rather than showing a stale profile.
        if (user is null || !user.IsActive)
        {
            return Result<UserDto>.Failure(Error.Unauthorized("Auth.NotAuthenticated", "Authentication is required."));
        }

        return Mapper.MapToDto(user);
    }
}

public sealed class GetUsersHandler : IQueryHandler<GetUsersQuery, PageResult<UserDto>>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private static readonly UserMapper Mapper = new();

    public GetUsersHandler(IRepository<ApplicationUser> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PageResult<UserDto>>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        var spec = new UsersPagedSpecification(query.Roles, query.ClassIds, query.Search, query.SortBy, query.SortDir, query.Page, query.PageSize);
        var pagedUsers = await _userRepository.ListPagedAsync(spec, ct);

        var items = pagedUsers.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<UserDto>(items, pagedUsers.Page, pagedUsers.PageSize, pagedUsers.Total);

        return result;
    }
}
