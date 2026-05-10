namespace MijnThuis.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string name, object key)
        : base($"Entity '{name}' with key '{key}' was not found.") { }
}
