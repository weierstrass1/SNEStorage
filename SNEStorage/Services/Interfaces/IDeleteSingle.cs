using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface IDeleteSingle<TKey, TModel> : IDeleteSingleDefinition<TKey, TModel>
{
    public Task<ServiceResultContext> Delete(
        TKey key, CancellationToken ct = default);

    public async Task<ServiceResultContext> DeleteSingle(
        TKey key, CancellationToken ct = default)
    {
        var findResult = await FindDelete(key, ct);

        if (!findResult.TryPickT0(out var item, out var findError))
            return findError;

        var validation = await ValidateDelete(item, ct);

        if (validation.Result != ServiceResult.Success)
            return validation;

        return await DeleteInternal(item, ct);
    }
}
