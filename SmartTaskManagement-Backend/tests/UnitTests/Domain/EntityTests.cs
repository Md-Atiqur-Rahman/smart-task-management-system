using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain;

public sealed class EntityTests
{
    [Fact]
    public void Entities_WithSameId_AreEqual()
    {
        var id = Guid.NewGuid();

        new TestEntity(id).ShouldBe(new TestEntity(id));
        (new TestEntity(id) == new TestEntity(id)).ShouldBeTrue();
    }

    [Fact]
    public void Entities_WithDifferentId_AreNotEqual()
    {
        (new TestEntity(Guid.NewGuid()) != new TestEntity(Guid.NewGuid()))
            .ShouldBeTrue();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        // Equals(object?) is a nullable parameter: must not throw when woven.
        new TestEntity(Guid.NewGuid()).Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void ValueObjects_WithSameComponents_AreEqual()
    {
        new TestMoney(10m, "EUR").ShouldBe(new TestMoney(10m, "EUR"));
        new TestMoney(10m, "EUR").ShouldNotBe(new TestMoney(10m, "USD"));
    }

    private sealed class TestEntity(Guid id) : Entity<Guid>(id);

    private sealed class TestMoney(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }
}
