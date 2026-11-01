namespace DPV.Domain.Entities;

public sealed class Artist : IEquatable<Artist>
{
    public Artist(string name)
    {
        Id = Guid.NewGuid();
        Name = EntityValidation.ValidateName(name, nameof(name));
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public void Rename(string name) =>
        Name = EntityValidation.ValidateName(name, nameof(name));

    public bool Equals(Artist? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Artist other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Artist? left, Artist? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(Artist? left, Artist? right) => !(left == right);
}
