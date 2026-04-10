using BarretApi.Core.Configuration;
using Shouldly;

namespace BarretApi.Core.UnitTests.Configuration;

public sealed class GitHubOptions_Validate_Tests
{
    [Fact]
    public void ReturnsCorrectDefaults_GivenNewInstance()
    {
        var options = new GitHubOptions();

        options.ClientId.ShouldBe(string.Empty);
        options.ClientSecret.ShouldBe(string.Empty);
        options.ApiBaseUrl.ShouldBe("https://api.github.com");
        options.OAuthBaseUrl.ShouldBe("https://github.com");
    }

    [Fact]
    public void ReturnsCorrectTokenStorageDefaults_GivenNewInstance()
    {
        var options = new GitHubOptions();

        options.TokenStorage.ShouldNotBeNull();
        options.TokenStorage.TableName.ShouldBe("githubtokens");
        options.TokenStorage.ConnectionString.ShouldBeNull();
        options.TokenStorage.AccountEndpoint.ShouldBe(string.Empty);
    }

    [Fact]
    public void ReturnsCorrectRepoStorageDefaults_GivenNewInstance()
    {
        var options = new GitHubOptions();

        options.RepoStorage.ShouldNotBeNull();
        options.RepoStorage.TableName.ShouldBe("githubrepositories");
        options.RepoStorage.ConnectionString.ShouldBeNull();
        options.RepoStorage.AccountEndpoint.ShouldBe(string.Empty);
    }

    [Fact]
    public void ReturnsSectionName_GivenConstant()
    {
        GitHubOptions.SectionName.ShouldBe("GitHub");
    }
}
