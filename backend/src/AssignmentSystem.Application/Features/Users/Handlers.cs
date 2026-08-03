using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;
using AssignmentSystem.Application.Features.Auth;

namespace AssignmentSystem.Application.Features.Users;

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IClassRosterRepository _classRosterRepository;
    private readonly ITeacherRosterRepository _teacherRosterRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly UserMapper Mapper = new();

    public CreateUserHandler(
        IRepository<ApplicationUser> userRepository,
        IRepository<Class> classRepository,
        IRepository<Department> departmentRepository,
        IClassRosterRepository classRosterRepository,
        ITeacherRosterRepository teacherRosterRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _classRepository = classRepository;
        _departmentRepository = departmentRepository;
        _classRosterRepository = classRosterRepository;
        _teacherRosterRepository = teacherRosterRepository;
        _passwordHasher = passwordHasher;
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

        string? teacherId = null;
        if (command.DepartmentId.HasValue)
        {
            var department = await _departmentRepository.GetByIdAsync(command.DepartmentId.Value, ct);
            if (department is null)
            {
                return Result<UserDto>.Failure(Error.NotFound("Department.NotFound", "The specified department was not found."));
            }

            if (command.Role == Role.Teacher)
            {
                var sequence = await _teacherRosterRepository.GetNextTeacherSequenceAsync(command.DepartmentId.Value, ct);
                teacherId = FormatTeacherId(department, sequence);
            }
        }

        var passwordHash = _passwordHasher.Hash(command.Password);

        try
        {
            var user = ApplicationUser.Create(
                command.Email,
                command.FullName,
                passwordHash,
                command.Role,
                command.ClassId,
                studentId,
                command.DepartmentId,
                teacherId);

            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch again with Class/Department included for full DTO mapping
            var fetchSpec = new UserWithClassByIdSpecification(user.Id);
            var savedUser = await _userRepository.FirstOrDefaultAsync(fetchSpec, ct);

            return Mapper.MapToDto(savedUser ?? user);
        }
        catch (DomainException ex)
        {
            return Result<UserDto>.Failure(Error.Validation("User.Invalid", ex.Message));
        }
    }

    /// <summary>
    /// The "IX-A" part of a student id — grade then section, where the grade is a Roman
    /// numeral by convention ("Class IX"), so it needs no letter prefix to read as one.
    /// The sequence that completes it is issued against this prefix, so the numbers are
    /// unique per grade+section. Falls back to the class name when grade/section weren't
    /// set, so creating a student never fails just because of that.
    /// </summary>
    private static string StudentIdPrefix(Class classObj) =>
        !string.IsNullOrWhiteSpace(classObj.Grade) && !string.IsNullOrWhiteSpace(classObj.Section)
            ? $"{classObj.Grade.Trim()}-{classObj.Section.Trim()}"
            : classObj.Name.Trim().Replace(' ', '-');

    /// <summary>"INS-SCI-01" — Instructor, department code, sequence. The department code
    /// is already short and bounded (10 chars), so it is used verbatim.</summary>
    private static string FormatTeacherId(Department department, int sequence) =>
        $"INS-{department.Code}-{sequence:D2}";
}

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly UserMapper Mapper = new();

    public UpdateUserHandler(
        IRepository<ApplicationUser> userRepository,
        IRepository<Class> classRepository,
        IRepository<Department> departmentRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _classRepository = classRepository;
        _departmentRepository = departmentRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        var fetchSpec = new UserWithClassByIdSpecification(command.Id);
        var user = await _userRepository.FirstOrDefaultAsync(fetchSpec, ct);
        if (user is null)
        {
            return Result<UserDto>.Failure(Error.NotFound("User.NotFound", "The specified user was not found."));
        }

        try
        {
            user.UpdateProfile(command.FullName);

            if (command.ClassId.HasValue && command.ClassId.Value != user.ClassId)
            {
                var classObj = await _classRepository.GetByIdAsync(command.ClassId.Value, ct);
                if (classObj is null)
                {
                    return Result<UserDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
                }
                user.AssignToClass(command.ClassId.Value);
            }

            if (command.DepartmentId.HasValue && command.DepartmentId.Value != user.DepartmentId)
            {
                var department = await _departmentRepository.GetByIdAsync(command.DepartmentId.Value, ct);
                if (department is null)
                {
                    return Result<UserDto>.Failure(Error.NotFound("Department.NotFound", "The specified department was not found."));
                }
                user.AssignToDepartment(command.DepartmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(command.Password))
            {
                user.SetPasswordHash(_passwordHasher.Hash(command.Password));
            }

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch again with Class included for full DTO mapping
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
        var spec = new UserWithClassByIdSpecification(query.Id);
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

        var spec = new UserWithClassByIdSpecification(userId);
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
        var spec = new UsersPagedSpecification(query.Role, query.ClassId, query.Search, query.Page, query.PageSize);
        var pagedUsers = await _userRepository.ListPagedAsync(spec, ct);

        var items = pagedUsers.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<UserDto>(items, pagedUsers.Page, pagedUsers.PageSize, pagedUsers.Total);

        return result;
    }
}
