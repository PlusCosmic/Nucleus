using FluentAssertions;
using Nucleus.Test.Builders;
using Nucleus.Test.Helpers;

namespace Nucleus.Test.Examples;

/// <summary>
/// Example tests demonstrating the test infrastructure setup.
/// These tests showcase how to use builders, helpers, and assertions.
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
    public void LinkBuilder_ShouldCreateValidLink()
    {
        // Arrange & Act
        var link = new LinkBuilder()
            .AsGitHub()
            .Build();

        // Assert
        link.Should().NotBeNull();
        link.Title.Should().Be("GitHub");
        link.Url.Should().Be("https://github.com");
    }

    [Fact]
    public void DiscordUserBuilder_ShouldCreateValidUser()
    {
        // Arrange & Act
        var user = new DiscordUserBuilder()
            .WithUsername("testuser")
            .WithGlobalName("Test User")
            .Build();

        // Assert
        user.Should().NotBeNull();
        user.Username.Should().Be("testuser");
        user.GlobalName.Should().Be("Test User");
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
