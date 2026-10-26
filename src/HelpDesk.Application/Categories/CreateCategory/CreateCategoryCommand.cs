using FluentValidation;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Categories.CreateCategory;

public sealed record CreateCategoryCommand(string Name) : IRequest<CategoryDto>;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.MaxNameLength);

        RuleFor(x => x.Name)
            .MustAsync(async (name, cancellationToken) => !await context.Categories.AnyAsync(c => c.Name == name, cancellationToken))
            .WithMessage("A category with this name already exists.")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));
    }
}

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = Category.Create(request.Name);
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CategoryDto.FromEntity(category);
    }
}
