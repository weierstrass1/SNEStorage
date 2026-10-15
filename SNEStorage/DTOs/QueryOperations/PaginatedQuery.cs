using SNEStorage.DTOs;

namespace SNEStorage.DTOs.QueryOperations;

public sealed record PaginatedQuery<TSort>(PaginationRequest Pagination, TSort Sort)
    where TSort : struct, Enum;
