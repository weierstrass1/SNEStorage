using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface IEditSingleDefinition<TKey, TEdit, TModel>
{
    public Task<ServiceResultContext> ValidateEdit(
        TEdit edit, CancellationToken ct = default);

    public Task<OneOf<TModel, ServiceResultContext>> FindEdit(
        TKey key, CancellationToken ct = default);

    public Task<ServiceResultContext> EditInternal(
        TModel item, TEdit edit, CancellationToken ct = default);
}
