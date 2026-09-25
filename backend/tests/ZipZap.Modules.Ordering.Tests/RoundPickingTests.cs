using FluentAssertions;
using Xunit;
using ZipZap.Modules.Ordering.Application;
using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Tests;

public class ShoppingListBuilderTests
{
    private static readonly Guid Milk = Guid.NewGuid(), Bread = Guid.NewGuid();

    private static PickLineDto Line(Guid orderId, string customer, Guid productId, string name, int qty,
        string unit = "szt", string status = "Pending", int picked = 0) =>
        new(orderId, ShoppingListBuilder.OrderCode(orderId), customer, Guid.NewGuid(), productId, name, unit, qty, true,
            PickDto.Pending(Guid.NewGuid()) with { Status = status, PickedQuantity = picked });

    [Fact]
    public void Same_product_from_many_orders_is_summed_and_every_order_stays_visible()
    {
        Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), o3 = Guid.NewGuid();
        var lines = new[]
        {
            Line(o1, "K-AAA", Milk, "Mleko 2%", 2),
            Line(o2, "K-BBB", Milk, "Mleko 2%", 1),
            Line(o2, "K-BBB", Bread, "Chleb", 1),
            Line(o3, "K-CCC", Bread, "Chleb", 3),
        };

        var list = ShoppingListBuilder.Build(lines);

        list.Select(r => r.ProductName).Should().Equal("Chleb", "Mleko 2%");
        var milk = list.Single(r => r.ProductId == Milk);
        milk.OrderedQuantity.Should().Be(3);
        milk.Lines.Select(l => (l.OrderId, l.CustomerCode, l.Quantity))
            .Should().BeEquivalentTo(new[] { (o1, "K-AAA", 2), (o2, "K-BBB", 1) });
        list.Single(r => r.ProductId == Bread).Lines.Select(l => l.OrderId).Should().BeEquivalentTo(new[] { o2, o3 });
        list.Sum(r => r.Lines.Count).Should().Be(lines.Length, "agregacja nie gubi żadnej pozycji");
    }

    [Fact]
    public void Different_units_of_the_same_product_are_separate_rows()
    {
        var o = Guid.NewGuid();
        var list = ShoppingListBuilder.Build(new[]
        {
            Line(o, "K-A", Milk, "Jabłka", 2, unit: "kg"),
            Line(o, "K-A", Milk, "Jabłka", 5, unit: "szt"),
        });
        list.Should().HaveCount(2);
        list.Select(r => (r.Unit, r.OrderedQuantity)).Should().BeEquivalentTo(new[] { ("kg", 2), ("szt", 5) });
    }

    [Fact]
    public void Bought_quantity_and_status_counters_follow_pick_state()
    {
        var o = Guid.NewGuid();
        var lines = new[]
        {
            Line(o, "K-A", Milk, "Mleko", 2, status: "Bought", picked: 2),
            Line(o, "K-B", Milk, "Mleko", 3, status: "Bought", picked: 1),
            Line(o, "K-C", Milk, "Mleko", 1, status: "Unavailable"),
            Line(o, "K-D", Milk, "Mleko", 1, status: "Substituted"),
            Line(o, "K-E", Milk, "Mleko", 4),
        };
        var row = ShoppingListBuilder.Build(lines).Single();
        row.OrderedQuantity.Should().Be(11);
        row.BoughtQuantity.Should().Be(3);
        (row.PendingLines, row.BoughtLines, row.UnavailableLines, row.SubstitutedLines).Should().Be((1, 2, 1, 1));

        var p = ShoppingListBuilder.Progress(lines);
        (p.Lines, p.Pending, p.Bought, p.Unavailable, p.Substituted).Should().Be((5, 1, 2, 1, 1));
    }

    [Fact]
    public void Empty_round_gives_empty_list_and_zero_progress()
    {
        ShoppingListBuilder.Build(Array.Empty<PickLineDto>()).Should().BeEmpty();
        ShoppingListBuilder.Progress(Array.Empty<PickLineDto>()).Should().Be(new RoundProgressDto(0, 0, 0, 0, 0));
    }
}

public class OrderItemPickTests
{
    private static readonly Guid Product = Guid.NewGuid(), Other = Guid.NewGuid(), Operator = Guid.NewGuid();
    private static readonly DateTime Now = new(2030, 4, 3, 10, 5, 0, DateTimeKind.Utc);

    private static OrderItemPick NewPick() => OrderItemPick.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Product);

    [Fact]
    public void Bought_records_quantity_author_time_and_bumps_version()
    {
        var p = NewPick();
        var change = p.Apply(p.Normalize(new PickRequest(PickStatus.Bought, 2)), Operator, "op@sklep.pl", Now);

        (p.Status, p.PickedQuantity, p.Version, p.UpdatedBy, p.UpdatedByLabel, p.UpdatedAtUtc)
            .Should().Be((PickStatus.Bought, 2, 1, (Guid?)Operator, "op@sklep.pl", (DateTime?)Now));
        (change.FromStatus, change.Status, change.Version, change.ChangedBy, change.ChangedAtUtc)
            .Should().Be((PickStatus.Pending, PickStatus.Bought, 1, Operator, Now));
    }

    [Fact]
    public void Unavailable_and_pending_clear_quantity_and_substitute()
    {
        var p = NewPick();
        p.Normalize(new PickRequest(PickStatus.Unavailable, 5, Other, "X", "szt", 2, "  brak  "))
            .Should().Be(new PickRequest(PickStatus.Unavailable, 0, Note: "brak"));
        p.Normalize(new PickRequest(PickStatus.Pending, 3)).Should().Be(new PickRequest(PickStatus.Pending, 0));
    }

    [Fact]
    public void Substitution_keeps_substitute_snapshot_and_zero_original_quantity()
    {
        var p = NewPick();
        p.Apply(p.Normalize(new PickRequest(PickStatus.Substituted, 7, Other, "Mleko 3,2%", "szt", 2, "klient zgodził się tel.")),
            Operator, null, Now);
        (p.Status, p.PickedQuantity, p.SubstituteProductId, p.SubstituteProductName, p.SubstituteQuantity, p.Note)
            .Should().Be((PickStatus.Substituted, 0, (Guid?)Other, "Mleko 3,2%", (int?)2, "klient zgodził się tel."));
    }

    [Theory]
    [InlineData(PickStatus.Bought, 0, null, null)]          // brak ilości
    [InlineData(PickStatus.Bought, 1001, null, null)]       // ponad limit
    [InlineData(PickStatus.Substituted, 0, null, 1)]        // brak zamiennika
    [InlineData(PickStatus.Substituted, 0, "same", 1)]      // zamiennik = ten sam produkt
    [InlineData(PickStatus.Substituted, 0, "other", 0)]     // brak ilości zamiennika
    public void Invalid_requests_are_rejected(PickStatus status, int qty, string? sub, int? subQty)
    {
        var p = NewPick();
        Guid? subId = sub switch { "same" => Product, "other" => Other, _ => null };
        FluentActions.Invoking(() => p.Normalize(new PickRequest(status, qty, subId, "Zamiennik", "szt", subQty)))
            .Should().Throw<OrderingDomainException>();
    }

    [Fact]
    public void Note_longer_than_limit_is_rejected()
        => FluentActions.Invoking(() => NewPick().Normalize(new PickRequest(PickStatus.Unavailable, 0,
                Note: new string('x', OrderItemPick.MaxNoteLength + 1))))
            .Should().Throw<OrderingDomainException>();

    [Fact]
    public void Identical_request_matches_current_state_so_double_submit_is_a_no_op()
    {
        var p = NewPick();
        var req = p.Normalize(new PickRequest(PickStatus.Bought, 2, Note: "ok"));
        p.Apply(req, Operator, null, Now);
        p.Matches(p.Normalize(new PickRequest(PickStatus.Bought, 2, Note: " ok "))).Should().BeTrue();
        p.Matches(p.Normalize(new PickRequest(PickStatus.Bought, 1, Note: "ok"))).Should().BeFalse();
    }
}
