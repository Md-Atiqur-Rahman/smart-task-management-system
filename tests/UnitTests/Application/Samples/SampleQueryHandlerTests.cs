// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Samples;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.GetSampleById;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;
using Datavanced.SmartTaskManagement.Domain.Samples;

using Microsoft.Extensions.Options;

using NSubstitute;

using SortDirection = Datavanced.SmartTaskManagement.Application.Abstractions.Paging.SortDirection;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Samples;

public sealed class SampleQueryHandlerTests
{
    private readonly ISampleReadRepository _readRepository = Substitute.For<ISampleReadRepository>();

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound()
    {
        var handler = new GetSampleByIdQueryHandler(_readRepository);

        var result = await handler.Handle(new GetSampleByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(SampleErrors.NotFound);
    }

    [Fact]
    public async Task GetById_Found_ReturnsDto()
    {
        var dto = new SampleDto(Guid.NewGuid(), "n", null, SampleStatus.Open, DateTimeOffset.UnixEpoch, null);
        _readRepository.GetByIdAsync(dto.Id, Arg.Any<CancellationToken>()).Returns(dto);
        var handler = new GetSampleByIdQueryHandler(_readRepository);

        var result = await handler.Handle(new GetSampleByIdQuery(dto.Id), CancellationToken.None);

        result.Value.ShouldBe(dto);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("createdAt")]
    [InlineData("status")]
    public void ListValidator_AcceptsWhitelistedSortFields(string sortBy)
    {
        var validator = new ListSamplesQueryValidator(
            Options.Create(new PagingOptions { DefaultPageSize = 20, MaxPageSize = 100 }));

        validator.Validate(new ListSamplesQuery(null, sortBy, SortDirection.Ascending, 1, 20))
            .IsValid.ShouldBeTrue();
    }
}
