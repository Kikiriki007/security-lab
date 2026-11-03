
using NUnit.Framework;
using Platform.LabMetadata;

namespace Platform.LabMetadata.Tests
{
    [TestFixture]
    public class LaboratoryTests
    {
        [Test]
        public void Rename_ShouldNotChangeId()
        {
            // Arrange - priprema laboratorije
            Guid id = Guid.NewGuid();

            var laboratory = new Laboratory(
                id,
                "Original name",
                "Learn access control",
                LaboratoryDifficulty.Easy,
                LaboratoryType.AccessControl,
                new[] { "security" }
            );

            // Act - promena naziva
            laboratory.Rename("Updated name");

            // Assert - provera rezultata
            Assert.That(laboratory.Id, Is.EqualTo(id));
            Assert.That(laboratory.Name, Is.EqualTo("Updated name"));
        }


        [Test]
        public void Constructor_ShouldRejectInvalidDifficulty()
        {
            // Arrange
            var invalidDifficulty = (LaboratoryDifficulty)999;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Laboratory(
                    Guid.NewGuid(),
                    "Test laboratory",
                    "Test objective",
                    invalidDifficulty,
                    LaboratoryType.AccessControl,
                    new[] { "security" }
                )
            );
        }


        [Test]
        public void Constructor_ShouldNormalizeTags()
        {
            // Arrange & Act
            var laboratory = new Laboratory(
                Guid.NewGuid(),
                "Test laboratory",
                "Test objective",
                LaboratoryDifficulty.Easy,
                LaboratoryType.AccessControl,
                new[] { " Security ", "MFA", "security", "  " }
            );

            // Assert
            Assert.That(
                laboratory.Tags,
                Is.EqualTo(new[] { "security", "mfa" })
            );
        }


        [Test]
        public void Constructor_ShouldRejectInvalidType()
        {
            // Arrange
            var invalidType = (LaboratoryType)999;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Laboratory(
                    Guid.NewGuid(),
                    "Test laboratory",
                    "Test objective",
                    LaboratoryDifficulty.Easy,
                    invalidType,
                    new[] { "security" }
                )
            );
        }


        [Test]
        public void Constructor_ShouldRejectEmptyId()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new Laboratory(
                    Guid.Empty,
                    "Test laboratory",
                    "Test objective",
                    LaboratoryDifficulty.Easy,
                    LaboratoryType.AccessControl,
                    new[] { "security" }
                )
            );
        }


        [Test]
        public void Constructor_ShouldRejectEmptyName()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new Laboratory(
                    Guid.NewGuid(),
                    "   ",
                    "Test objective",
                    LaboratoryDifficulty.Easy,
                    LaboratoryType.AccessControl,
                    new[] { "security" }
                )
            );
        }


        [Test]
        public void Constructor_ShouldRejectEmptyObjective()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new Laboratory(
                    Guid.NewGuid(),
                    "Test laboratory",
                    "   ",
                    LaboratoryDifficulty.Easy,
                    LaboratoryType.AccessControl,
                    new[] { "security" }
                )
            );
        }


        [Test]
        public void Rename_ShouldRejectEmptyName()
        {
            // Arrange
            var laboratory = new Laboratory(
                Guid.NewGuid(),
                "Original name",
                "Test objective",
                LaboratoryDifficulty.Easy,
                LaboratoryType.AccessControl,
                new[] { "security" }
            );

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                laboratory.Rename("   ")
            );

            Assert.That(laboratory.Name, Is.EqualTo("Original name"));
        }


        [Test]
        public void Constructor_ShouldRejectNullTags()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new Laboratory(
                    Guid.NewGuid(),
                    "Test laboratory",
                    "Test objective",
                    LaboratoryDifficulty.Easy,
                    LaboratoryType.AccessControl,
                    null!
                )
            );
        }

    }
}
