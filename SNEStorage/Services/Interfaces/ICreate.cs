using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface ICreate<TCreate, TModel> : ICreateDefinition<TCreate, TModel>
{
    public Task<OneOf<TModel, ServiceResultContext>> Create(
        TCreate create, CancellationToken ct = default);

    public async Task<OneOf<TModel, ServiceResultContext>> CreateItem(
        TCreate create, CancellationToken ct = default)
    {
        var validation = await ValidateCreate(create, ct);

        if (validation.Result != ServiceResult.Success)
            return validation;

        return await CreateInternal(create, ct);
    }
}
