using System;
using System.Collections.Generic;
using System.Linq;

namespace Platform.LabMetadata
{
    public class Laboratory
    {
        public Guid Id { get; }
        public string Name { get; private set; }
        public string Objective { get; private set; }
        public LaboratoryDifficulty Difficulty { get; private set; }
        public LaboratoryType Type { get; private set; }
        public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();

        public Laboratory(
            Guid id,
            string name,
            string objective,
            LaboratoryDifficulty difficulty,
            LaboratoryType type,
            IEnumerable<string> tags)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Laboratory ID cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Laboratory name is required.", nameof(name));

            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("Laboratory objective is required.", nameof(objective));

            if (!Enum.IsDefined(difficulty))
                throw new ArgumentOutOfRangeException(nameof(difficulty));

            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(nameof(type));

            Id = id;
            Name = name.Trim();
            Objective = objective.Trim();
            Difficulty = difficulty;
            Type = type;

            if (tags is null)
                throw new ArgumentNullException(nameof(tags));

            Tags = Array.AsReadOnly(
                tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToArray()
            );
        }

        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException(
                    "Laboratory name is required.",
                    nameof(newName));

            Name = newName.Trim();
        }
    }
}
