namespace RomaERP.Application.Common.Exceptions;

public class ValidationAppException : Exception
{
    public ValidationAppException(string message) : base(message)
    {
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} ({key}) was not found.")
    {
    }
}
