namespace SocialDDD.Domain.Exceptions;

public sealed class DuplicateRepostException(string message) : DomainException(message);
