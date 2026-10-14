namespace SNEStorage.Services.Interfaces;

public interface IHaveId<TKey>
{
    TKey Id { get; }
}
public interface IHaveName
{
    string Name { get; set; }
}
public interface IHaveCreatedAt
{
    DateTime CreatedAt { get; set; }
}
public interface IHaveUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}
