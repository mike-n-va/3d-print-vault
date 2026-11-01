using System.Collections.ObjectModel;

namespace DPV.Domain.Entities;

public sealed class Model : IEquatable<Model>
{
    public Model(string name)
        : this(name, null, Array.Empty<Tag>())
    {
    }

    public Model(string name, Collection? collection)
        : this(name, collection, Array.Empty<Tag>())
    {
    }

    public Model(string name, Collection? collection, IEnumerable<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        Id = Guid.NewGuid();
        Name = EntityValidation.ValidateName(name, nameof(name));
        Collection = collection;

        var tagList = tags.ToList();
        if (tagList.Any(tag => tag is null))
        {
            throw new ArgumentException("Tags cannot contain null entries.", nameof(tags));
        }

        if (tagList.Select(tag => tag.Id).Distinct().Count() != tagList.Count)
        {
            throw new ArgumentException("A Model cannot contain duplicate Tag identities.", nameof(tags));
        }

        Tags = new ReadOnlyCollection<Tag>(tagList);
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Collection? Collection { get; }

    public IReadOnlyList<Tag> Tags { get; }

    public void Rename(string name) =>
        Name = EntityValidation.ValidateName(name, nameof(name));

    public bool Equals(Model? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Model other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Model? left, Model? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(Model? left, Model? right) => !(left == right);
}
