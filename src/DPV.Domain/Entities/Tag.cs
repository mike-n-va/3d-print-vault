namespace DPV.Domain.Entities;

public sealed class Tag : IEquatable<Tag>
{
    public Tag(string name)
    {
        Id = Guid.NewGuid();
        Name = EntityValidation.ValidateName(name, nameof(name));
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public void Rename(string name) =>
        Name = EntityValidation.ValidateName(name, nameof(name));

    public bool Equals(Tag? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Tag other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Tag? left, Tag? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(Tag? left, Tag? right) => !(left == right);
}
