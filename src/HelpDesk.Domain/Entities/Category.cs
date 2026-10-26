namespace HelpDesk.Domain.Entities;

public class Category
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private Category()
    {
    }

    public static Category Create(string name)
    {
        ValidateName(name);

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        ValidateName(name);
        Name = name;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name cannot be empty.", nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Category name cannot exceed {MaxNameLength} characters.", nameof(name));
        }
    }
}
