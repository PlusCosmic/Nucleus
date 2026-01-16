using FluentAssertions;
using Nucleus.Clips.Test.Builders;
using Nucleus.Clips.Test.Helpers;

namespace Nucleus.Clips.Test.Examples;

/// <summary>
/// Example tests demonstrating the test infrastructure setup.
/// </summary>
public class ExampleTests
{
    [Fact]
    public void ClipBuilder_ShouldCreateValidClip()
    {
        // Arrange & Act
        var clip = new ClipBuilder()
            .WithOwnerId(Guid.NewGuid())
            .AsApexRanked()
            .Build();

        // Assert
        clip.Should().NotBeNull();
        clip.Tags.Should().Contain("ranked");
        clip.Tags.Should().Contain("apex");
    }

    [Fact]
    public void AuthHelper_ShouldCreateTestUser()
    {
        // Arrange & Act
        var principal = AuthHelper.CreateTestUser(
            discordId: "123456789012345678",
            username: "testuser",
            globalName: "Test User"
        );

        // Assert
        principal.Should().NotBeNull();
        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ClipBuilder_CreateMany_ShouldCreateMultipleClips()
    {
        // Arrange & Act
        var clips = ClipBuilder.CreateMany(5).ToList();

        // Assert
        clips.Should().HaveCount(5);
        clips.Should().OnlyHaveUniqueItems(c => c.ClipId);
    }
}
