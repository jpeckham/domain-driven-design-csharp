namespace SocialDDD.Domain.Exceptions;

public sealed class NotLikedException(string message) : DomainException(message);
