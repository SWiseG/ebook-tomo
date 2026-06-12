namespace Ebook.Domain.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
