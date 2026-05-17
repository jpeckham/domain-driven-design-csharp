namespace SocialDDD.Domain.Exceptions;

public sealed class DomainValidationException(string message) : DomainException(message);
