namespace SocialDDD.Domain.Exceptions;

public sealed class AlreadyLikedException(string message) : DomainException(message);
