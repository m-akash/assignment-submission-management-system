using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Groups;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Groups;

public sealed class CreateGroupHandler : ICommandHandler<CreateGroupCommand, GroupDto>
{
    private readonly IRepository<Group> _groupRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly GroupMapper Mapper = new();

    public CreateGroupHandler(IRepository<Group> groupRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GroupDto>> HandleAsync(CreateGroupCommand command, CancellationToken ct = default)
    {
        var codeSpec = new GroupByCodeSpecification(command.Code);
        if (await _groupRepository.AnyAsync(codeSpec, ct))
        {
            return Result<GroupDto>.Failure(
                Error.Conflict("Group.CodeAlreadyExists", "A group with this code already exists."));
        }

        try
        {
            var group = Group.Create(command.Name, command.Code);
            await _groupRepository.AddAsync(group, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(group);
        }
        catch (DomainException ex)
        {
            return Result<GroupDto>.Failure(Error.Validation("Group.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateGroupHandler : ICommandHandler<UpdateGroupCommand, GroupDto>
{
    private readonly IRepository<Group> _groupRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly GroupMapper Mapper = new();

    public UpdateGroupHandler(IRepository<Group> groupRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GroupDto>> HandleAsync(UpdateGroupCommand command, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(command.Id, ct);
        if (group is null)
        {
            return Result<GroupDto>.Failure(
                Error.NotFound("Group.NotFound", "The specified group was not found."));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (normalizedCode != group.Code)
        {
            var codeSpec = new GroupByCodeSpecification(command.Code);
            if (await _groupRepository.AnyAsync(codeSpec, ct))
            {
                return Result<GroupDto>.Failure(
                    Error.Conflict("Group.CodeAlreadyExists", "A group with this code already exists."));
            }
        }

        try
        {
            group.Update(command.Name, command.Code);
            _groupRepository.Update(group);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(group);
        }
        catch (DomainException ex)
        {
            return Result<GroupDto>.Failure(Error.Validation("Group.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteGroupHandler : ICommandHandler<DeleteGroupCommand>
{
    private readonly IRepository<Group> _groupRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGroupHandler(IRepository<Group> groupRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteGroupCommand command, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(command.Id, ct);
        if (group is null)
        {
            return Result.Failure(Error.NotFound("Group.NotFound", "The specified group was not found."));
        }

        // Students reference groups with RESTRICT, so a group that still has students is
        // refused by the database and surfaces as a 409. Nulling them out instead would
        // leave class IX+ students without the group they are required to have.
        _groupRepository.Remove(group);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetGroupByIdHandler : IQueryHandler<GetGroupByIdQuery, GroupDto>
{
    private readonly IRepository<Group> _groupRepository;
    private static readonly GroupMapper Mapper = new();

    public GetGroupByIdHandler(IRepository<Group> groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Result<GroupDto>> HandleAsync(GetGroupByIdQuery query, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(query.Id, ct);
        if (group is null)
        {
            return Result<GroupDto>.Failure(
                Error.NotFound("Group.NotFound", "The specified group was not found."));
        }

        return Mapper.MapToDto(group);
    }
}

public sealed class GetGroupsHandler : IQueryHandler<GetGroupsQuery, PageResult<GroupDto>>
{
    private readonly IRepository<Group> _groupRepository;
    private static readonly GroupMapper Mapper = new();

    public GetGroupsHandler(IRepository<Group> groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Result<PageResult<GroupDto>>> HandleAsync(GetGroupsQuery query, CancellationToken ct = default)
    {
        var spec = new GroupsPagedSpecification(query.Search, query.Page, query.PageSize);
        var paged = await _groupRepository.ListPagedAsync(spec, ct);

        var items = paged.Items.Select(Mapper.MapToDto).ToList();
        return new PageResult<GroupDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}
