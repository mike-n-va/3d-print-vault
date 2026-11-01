namespace DPV.Domain.Entities;

public sealed class Collection : IEquatable<Collection>
{
    public Collection(string name, Artist? artist = null)
    {
        Id = Guid.NewGuid();
        Name = EntityValidation.ValidateName(name, nameof(name));
        Artist = artist;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Artist? Artist { get; }

    public void Rename(string name) =>
        Name = EntityValidation.ValidateName(name, nameof(name));

    public bool Equals(Collection? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Collection other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Collection? left, Collection? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(Collection? left, Collection? right) => !(left == right);
}
