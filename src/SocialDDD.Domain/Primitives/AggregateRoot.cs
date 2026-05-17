namespace SocialDDD.Domain.Primitives;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull;
