using OneOf;

namespace SNEStorage.Services.Interfaces;
public interface ICreate<TCreate, TModel, TResult> where TResult : Enum
{
    public Task<OneOf<TModel, TResult>> Create(TCreate create, CancellationToken ct = default);
    public Task<TResult> ValidateCreate(TCreate create, CancellationToken ct = default);
    public Task<OneOf<TModel, TResult>> CreateInternal(TCreate create, CancellationToken ct = default);
    public async Task<OneOf<TModel, TResult>> CreateItem(TCreate create, TResult success, CancellationToken ct = default)
    {
        var validation = await ValidateCreate(create, ct);
        if (!validation.Equals(success))
            return validation;
        var createRes = await CreateInternal(create, ct);
        return createRes.TryPickT0(out var item, out var createError) ?
            item : createError;
    }
}
