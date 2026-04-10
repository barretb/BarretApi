using BarretApi.Api.Features.GitHub;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.GitHub;

public sealed class GitHubCreateIssueValidator_Tests
{
    private readonly GitHubCreateIssueValidator _validator = new();

    [Fact]
    public async Task FailsValidation_GivenEmptyTitle()
    {
        var request = new GitHubCreateIssueRequest { Name = "my-repo", Title = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Title");
    }

    [Fact]
    public async Task FailsValidation_GivenEmptyRepoName()
    {
        var request = new GitHubCreateIssueRequest { Name = "", Title = "Fix bug" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task PassesValidation_GivenValidRequest()
    {
        var request = new GitHubCreateIssueRequest { Name = "my-repo", Title = "Fix bug" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task PassesValidation_GivenOptionalFields()
    {
        var request = new GitHubCreateIssueRequest
        {
            Name = "my-repo",
            Title = "Add dark mode",
            Body = "## Description\nPlease add dark mode.",
            Labels = ["enhancement", "ui"]
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }
}
