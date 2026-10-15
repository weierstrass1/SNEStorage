using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;
public partial class FlagsService(SnestorageContext context,
        AccountService accountService,
        DBHelperService dbHelperService,
        PaginatedListQueryOperation<FlagReadDTO, FlagSort> getPage,
        GetByPropertyQueryOperation<Flag, long> getById,
        GetByPropertyQueryOperation<Reason, long> getReasonById,
        GetByPropertyQueryOperation<Resource, long> getResourceById,
        GetByPropertyQueryOperation<Comment, long> getCommentById,
        ILogger<FlagsService> logger) : 
    IFullGet<Flag, FlagFilterDTO, FlagSort, FlagReadDTO>,
    IEditSingle<long, bool, Flag>,
    ICreate<ResourceFlagCreateDTO, Flag>,
    ICreate<CommentFlagCreateDTO, Flag>
{
    public const int MAX_AMOUNT_PER_PAGE = 20;
    private readonly SnestorageContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly PaginatedListQueryOperation<FlagReadDTO, FlagSort> _getPage = getPage;
    private readonly GetByPropertyQueryOperation<Flag, long> _getById = getById;
    private readonly GetByPropertyQueryOperation<Reason, long> _getReasonById = getReasonById;
    private readonly GetByPropertyQueryOperation<Resource, long> _getResourceById = getResourceById;
    private readonly GetByPropertyQueryOperation<Comment, long> _getCommentById = getCommentById;
    private readonly ILogger<FlagsService> _logger = logger;

    public Task<OneOf<Flag, ServiceResultContext>> Create(ResourceFlagCreateDTO create, CancellationToken ct = default)
    {
        ICreate<ResourceFlagCreateDTO, Flag> creator = this;
        return creator.CreateItem(create, ct);
    }

    public Task<OneOf<Flag, ServiceResultContext>> Create(CommentFlagCreateDTO create, CancellationToken ct = default)
    {
        ICreate<CommentFlagCreateDTO, Flag> creator = this;
        return creator.CreateItem(create, ct);
    }

    public Task<OneOf<Flag, ServiceResultContext>> Edit(long key, bool edit, CancellationToken ct = default)
    {
        IEditSingle<long, bool, Flag> editor = this;
        return editor.EditSingle(key, edit, ct);
    }
    public Task<OneOf<List<FlagReadDTO>, ServiceResultContext>> FullGet(FlagFilterDTO filter, FlagSort sort, int maxAmount = 10, int page = 0, CancellationToken ct = default)
    {
        var query = GetFilter(_context.Flags.AsNoTracking(), filter);
        var resultQuery = GetSelect(GetSort(query, sort));
        var parameter = new PaginatedQuery<FlagSort>(
            new PaginationRequest(maxAmount, MAX_AMOUNT_PER_PAGE, page), sort);

        return _getPage.Execute(resultQuery, parameter, ct: ct);
    }
}
