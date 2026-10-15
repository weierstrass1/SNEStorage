using SNEStorage.DTOs;

namespace SNEStorage.DTOs.QueryOperations;

public sealed record GetByIdQuery<TKey>(TKey Id, ServiceResult NotFound);
