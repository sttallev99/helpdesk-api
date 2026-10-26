using HelpDesk.Domain.Entities;

namespace HelpDesk.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, bool IsActive)
{
    public static CategoryDto FromEntity(Category category) => new(category.Id, category.Name, category.IsActive);
}
