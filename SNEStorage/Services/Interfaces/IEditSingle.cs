using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface IEditSingle<TKey, TEdit, TModel>
{
    public Task<OneOf<TModel, ServiceResultContext>> Edit(
        TKey key, TEdit edit, CancellationToken ct = default);

    public Task<ServiceResultContext> ValidateEdit(
        TEdit edit, CancellationToken ct = default);

    public Task<OneOf<TModel, ServiceResultContext>> FindEdit(
        TKey key, CancellationToken ct = default);

    public Task<ServiceResultContext> EditInternal(
        TModel item, TEdit edit, CancellationToken ct = default);

    public async Task<OneOf<TModel, ServiceResultContext>> EditSingle(
        TKey key, TEdit edit, CancellationToken ct = default)
    {
        var validation = await ValidateEdit(edit, ct);

        if (validation.Result != ServiceResult.Success)
            return validation;

        var findResult = await FindEdit(key, ct);

        if (!findResult.TryPickT0(out var item, out var findError))
            return findError;

        var editResult = await EditInternal(item, edit, ct);

        return editResult.Result == ServiceResult.Success ?
            item : editResult;
    }
}
