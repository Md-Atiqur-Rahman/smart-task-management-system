// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Domain.Results;
using Contoso.TemplateApp.Domain.Samples;

namespace Contoso.TemplateApp.UnitTests.Domain;

public sealed class SampleTests
{
    [Fact]
    public void Create_SetsOpenStatusAndValues()
    {
        var sample = Sample.Create("name", null);

        sample.Id.ShouldNotBe(Guid.Empty);
        sample.Name.ShouldBe("name");
        sample.Description.ShouldBeNull();
        sample.Status.ShouldBe(SampleStatus.Open);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_BlankName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => Sample.Create(name, null));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        Should.Throw<ArgumentException>(
            () => Sample.Create(new string('x', Sample.NameMaxLength + 1), null));
    }

    [Fact]
    public void Complete_Open_Succeeds()
    {
        var sample = Sample.Create("name", null);

        sample.Complete().IsSuccess.ShouldBeTrue();
        sample.Status.ShouldBe(SampleStatus.Completed);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ReturnsConflict()
    {
        var sample = Sample.Create("name", null);
        sample.Complete();

        var result = sample.Complete();

        result.Error.ShouldBe(SampleErrors.AlreadyCompleted);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void Delete_MarksDeletedWithTime()
    {
        var sample = Sample.Create("name", null);
        var now = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

        sample.Delete(now);

        sample.IsDeleted.ShouldBeTrue();
        sample.DeletedAt.ShouldBe(now);
    }
}
