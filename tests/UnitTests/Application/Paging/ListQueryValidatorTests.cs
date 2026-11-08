using Contoso.TemplateApp.Application.Abstractions.Paging;

using Microsoft.Extensions.Options;

using SortDirection = Contoso.TemplateApp.Application.Abstractions.Paging.SortDirection;

namespace Contoso.TemplateApp.UnitTests.Application.Paging;

public sealed class ListQueryValidatorTests
{
    private static readonly IOptions<PagingOptions> Options =
        Microsoft.Extensions.Options.Options.Create(
            new PagingOptions { DefaultPageSize = 20, MaxPageSize = 100 });

    private readonly TestListQueryValidator _validator = new(Options);

    [Fact]
    public void ValidQuery_Passes()
    {
        _validator.Validate(Query(sortBy: "name")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SortBy_IsCaseInsensitive()
    {
        _validator.Validate(Query(sortBy: "NAME")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void NoSortBy_Passes()
    {
        _validator.Validate(Query(sortBy: null)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("password")]
    [InlineData("")]
    [InlineData("name; DROP TABLE x")]
    public void UnknownSortBy_Fails(string sortBy)
    {
        var result = _validator.Validate(Query(sortBy: sortBy));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.SortBy));
    }

    [Fact]
    public void PageSizeAboveMax_Fails()
    {
        var result = _validator.Validate(Query(pageSize: 101));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.PageSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageSizeBelowOne_Fails(int pageSize)
    {
        _validator.Validate(Query(pageSize: pageSize)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageBelowOne_Fails(int page)
    {
        var result = _validator.Validate(Query(page: page));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.Page));
    }

    [Fact]
    public void UndefinedSortDirection_Fails()
    {
        var result = _validator.Validate(Query(sortDirection: (SortDirection)42));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.SortDirection));
    }

    private static TestListQuery Query(
        string? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending,
        int page = 1,
        int pageSize = 20) =>
        new(null, sortBy, sortDirection, page, pageSize);

    private sealed record TestListQuery(
        string? Search, string? SortBy, SortDirection SortDirection, int Page, int PageSize)
        : ListQuery(Search, SortBy, SortDirection, Page, PageSize);

    private sealed class TestListQueryValidator(IOptions<PagingOptions> options)
        : ListQueryValidator<TestListQuery>(options, ["name", "createdAt"]);
}
