using OneOf;

namespace SNEStorage.Services.Interfaces;
public interface IEditSingle<TKey, TEdit, TModel, TResult> 
    where TResult : Enum
{
    public Task<OneOf<TModel, TResult>> Edit(TKey key, TEdit edit, CancellationToken ct = default);
    public Task<TResult> ValidateEdit(TEdit edit, CancellationToken ct = default);
    public Task<OneOf<TModel?, TResult>> FindEdit(TKey key, CancellationToken ct = default);
    public Task<TResult> EditInternal(TModel item, TEdit edit, CancellationToken ct = default);
    public async Task<OneOf<TModel, TResult>> EditSingle(TKey key, TEdit edit, TResult success, TResult notFound, TResult error, CancellationToken ct = default)
    {
        var validation = await ValidateEdit(edit, ct);
        if (!validation.Equals(success))
            return validation;

        var findRes = await FindEdit(key, ct);
        if (!findRes.TryPickT0(out var result, out var findError))
            return findError;

        if (result is null)
            return notFound;

        var editRes = await EditInternal(result, edit, ct);;

        return editRes.Equals(success) ?
            result : editRes;
    }
}
