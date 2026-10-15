using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface IDeleteSingleDefinition<TKey, TModel>
{
    public Task<OneOf<TModel, ServiceResultContext>> FindDelete(
        TKey key, CancellationToken ct = default);

    public Task<ServiceResultContext> ValidateDelete(
        TModel item, CancellationToken ct = default);

    public Task<ServiceResultContext> DeleteInternal(
        TModel item, CancellationToken ct = default);
}
