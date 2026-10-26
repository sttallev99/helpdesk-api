using HelpDesk.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Categories.ListCategories;

/// <summary>IncludeInactive is intended for the admin management screen only.</summary>
public sealed record ListCategoriesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyCollection<CategoryDto>>;

public sealed class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public ListCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<CategoryDto>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
