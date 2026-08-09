using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.AcademicYears;

public sealed class CreateAcademicYearHandler : ICommandHandler<CreateAcademicYearCommand, AcademicYearDto>
{
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AcademicYearMapper Mapper = new();

    public CreateAcademicYearHandler(IRepository<AcademicYear> academicYearRepository, IUnitOfWork unitOfWork)
    {
        _academicYearRepository = academicYearRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AcademicYearDto>> HandleAsync(CreateAcademicYearCommand command, CancellationToken ct = default)
    {
        var nameSpec = new AcademicYearByNameSpecification(command.Name);
        if (await _academicYearRepository.AnyAsync(nameSpec, ct))
        {
            return Result<AcademicYearDto>.Failure(Error.Conflict(
                "AcademicYear.NameAlreadyExists", "An academic year with this name already exists."));
        }

        try
        {
            if (command.IsCurrent)
            {
                await CurrentYearFlag.ReleaseAsync(_academicYearRepository, _unitOfWork, excludingId: null, ct);
            }

            var academicYear = AcademicYear.Create(command.Name, command.StartDate, command.EndDate, command.IsCurrent);
            await _academicYearRepository.AddAsync(academicYear, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // A year that has just been created has nothing enrolled against it.
            return Mapper.MapToDto(academicYear) with { EnrollmentCount = 0 };
        }
        catch (DomainException ex)
        {
            return Result<AcademicYearDto>.Failure(Error.Validation("AcademicYear.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateAcademicYearHandler : ICommandHandler<UpdateAcademicYearCommand, AcademicYearDto>
{
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IAcademicYearUsageReader _usageReader;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AcademicYearMapper Mapper = new();

    public UpdateAcademicYearHandler(
        IRepository<AcademicYear> academicYearRepository,
        IAcademicYearUsageReader usageReader,
        IUnitOfWork unitOfWork)
    {
        _academicYearRepository = academicYearRepository;
        _usageReader = usageReader;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AcademicYearDto>> HandleAsync(UpdateAcademicYearCommand command, CancellationToken ct = default)
    {
        var academicYear = await _academicYearRepository.GetByIdAsync(command.Id, ct);
        if (academicYear is null)
        {
            return Result<AcademicYearDto>.Failure(Error.NotFound(
                "AcademicYear.NotFound", "The specified academic year was not found."));
        }

        var nameSpec = new AcademicYearByNameSpecification(command.Name, excludingId: command.Id);
        if (await _academicYearRepository.AnyAsync(nameSpec, ct))
        {
            return Result<AcademicYearDto>.Failure(Error.Conflict(
                "AcademicYear.NameAlreadyExists", "An academic year with this name already exists."));
        }

        try
        {
            academicYear.Update(command.Name, command.StartDate, command.EndDate);

            if (command.IsCurrent && !academicYear.IsCurrent)
            {
                await CurrentYearFlag.ReleaseAsync(_academicYearRepository, _unitOfWork, command.Id, ct);
                academicYear.MarkAsCurrent();
            }
            else if (!command.IsCurrent && academicYear.IsCurrent)
            {
                // Allowed, and left as-is rather than moved elsewhere: between sessions a
                // school genuinely has no current year, and guessing which one to promote
                // would be worse than showing none.
                academicYear.ClearCurrent();
            }

            _academicYearRepository.Update(academicYear);
            await _unitOfWork.SaveChangesAsync(ct);

            var counts = await _usageReader.GetEnrollmentCountsAsync([academicYear.Id], ct);
            return Mapper.MapToDto(academicYear) with { EnrollmentCount = counts.GetValueOrDefault(academicYear.Id) };
        }
        catch (DomainException ex)
        {
            return Result<AcademicYearDto>.Failure(Error.Validation("AcademicYear.Invalid", ex.Message));
        }
    }
}

/// <summary>
/// Deletes a year that nothing is enrolled against. Refused otherwise: the year is what
/// tells a class roster which session it describes, so removing one that enrollments point
/// at would strip the meaning from rows it does not own. The foreign key is
/// <c>DeleteBehavior.Restrict</c> for the same reason — this check exists to turn that into
/// an explanation rather than a database error.
/// </summary>
public sealed class DeleteAcademicYearHandler : ICommandHandler<DeleteAcademicYearCommand>
{
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAcademicYearHandler(
        IRepository<AcademicYear> academicYearRepository,
        IRepository<StudentEnrollment> enrollmentRepository,
        IUnitOfWork unitOfWork)
    {
        _academicYearRepository = academicYearRepository;
        _enrollmentRepository = enrollmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteAcademicYearCommand command, CancellationToken ct = default)
    {
        var academicYear = await _academicYearRepository.GetByIdAsync(command.Id, ct);
        if (academicYear is null)
        {
            return Result.Failure(Error.NotFound(
                "AcademicYear.NotFound", "The specified academic year was not found."));
        }

        var inUseSpec = new EnrollmentsByAcademicYearSpecification(command.Id);
        if (await _enrollmentRepository.AnyAsync(inUseSpec, ct))
        {
            return Result.Failure(Error.Conflict(
                "AcademicYear.InUse",
                "This academic year has enrollments recorded against it and cannot be deleted."));
        }

        _academicYearRepository.Remove(academicYear);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetAcademicYearByIdHandler : IQueryHandler<GetAcademicYearByIdQuery, AcademicYearDto>
{
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IAcademicYearUsageReader _usageReader;
    private static readonly AcademicYearMapper Mapper = new();

    public GetAcademicYearByIdHandler(
        IRepository<AcademicYear> academicYearRepository,
        IAcademicYearUsageReader usageReader)
    {
        _academicYearRepository = academicYearRepository;
        _usageReader = usageReader;
    }

    public async Task<Result<AcademicYearDto>> HandleAsync(GetAcademicYearByIdQuery query, CancellationToken ct = default)
    {
        var academicYear = await _academicYearRepository.GetByIdAsync(query.Id, ct);
        if (academicYear is null)
        {
            return Result<AcademicYearDto>.Failure(Error.NotFound(
                "AcademicYear.NotFound", "The specified academic year was not found."));
        }

        var counts = await _usageReader.GetEnrollmentCountsAsync([academicYear.Id], ct);
        return Mapper.MapToDto(academicYear) with { EnrollmentCount = counts.GetValueOrDefault(academicYear.Id) };
    }
}

public sealed class GetAcademicYearsHandler : IQueryHandler<GetAcademicYearsQuery, PageResult<AcademicYearDto>>
{
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IAcademicYearUsageReader _usageReader;
    private static readonly AcademicYearMapper Mapper = new();

    public GetAcademicYearsHandler(
        IRepository<AcademicYear> academicYearRepository,
        IAcademicYearUsageReader usageReader)
    {
        _academicYearRepository = academicYearRepository;
        _usageReader = usageReader;
    }

    public async Task<Result<PageResult<AcademicYearDto>>> HandleAsync(GetAcademicYearsQuery query, CancellationToken ct = default)
    {
        var spec = new AcademicYearsPagedSpecification(query.Search, query.SortBy, query.SortDir, query.Page, query.PageSize);
        var paged = await _academicYearRepository.ListPagedAsync(spec, ct);

        // One grouped query for the page, not one per row.
        var counts = await _usageReader.GetEnrollmentCountsAsync(
            paged.Items.Select(y => y.Id).ToList(), ct);

        var items = paged.Items
            .Select(y => Mapper.MapToDto(y) with { EnrollmentCount = counts.GetValueOrDefault(y.Id) })
            .ToList();

        return new PageResult<AcademicYearDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}

/// <summary>
/// Releasing the "current session" flag from whichever year holds it, so a new one can take
/// it. Shared by create and update because both have to do exactly this first.
///
/// Committed on its own, before the caller's own save, rather than in the same transaction.
/// The partial unique index on <c>is_current</c> is checked per statement, and EF Core does
/// not promise the clear is written before the claim when both are updates to the same
/// table — so batching them is a constraint violation waiting for the wrong row ordering.
/// The cost of splitting them is a moment where no year is flagged, and if the caller's save
/// then fails the school is left with none. That is visible in the UI and one click to fix,
/// which is the better failure than a request that breaks on a race nobody can reproduce.
/// </summary>
internal static class CurrentYearFlag
{
    public static async Task ReleaseAsync(
        IRepository<AcademicYear> repository,
        IUnitOfWork unitOfWork,
        Guid? excludingId,
        CancellationToken ct)
    {
        var currentSpec = new CurrentAcademicYearSpecification(excludingId);
        var holders = await repository.ListAsync(currentSpec, ct);
        if (holders.Count == 0)
        {
            return;
        }

        foreach (var holder in holders)
        {
            holder.ClearCurrent();
            repository.Update(holder);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
