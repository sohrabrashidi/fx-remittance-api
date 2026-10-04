using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Core.Tests;

public class TransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Party Sender = new("Ahmed Al-Sabah", "KW");
    private static readonly Party Recipient = new("Priya Nair", "IN", "IN00HDFC0001234567");

    private static Quote SampleQuote(DateTimeOffset? expiresAt = null)
    {
        var kwd = CurrencyCode.Parse("KWD");
        var inr = CurrencyCode.Parse("INR");
        return new Quote(
            Guid.NewGuid(),
            "KWD-INR",
            new Money.Money(100m, kwd),
            new Money.Money(1m, kwd),
            new Money.Money(101m, kwd),
            280m,
            278.6m,
            new Money.Money(27860m, inr),
            Now,
            expiresAt ?? Now.AddMinutes(10));
    }

    [Fact]
    public void New_transfer_starts_as_created_with_a_reference()
    {
        var transfer = Transfer.Create(SampleQuote(), Sender, Recipient, Now);

        Assert.Equal(TransferStatus.Created, transfer.Status);
        Assert.StartsWith("TR260901-", transfer.Reference);
    }

    [Fact]
    public void Expired_quote_cannot_be_used()
    {
        var quote = SampleQuote(expiresAt: Now.AddMinutes(-1));

        var ex = Assert.Throws<TransferRuleException>(() => Transfer.Create(quote, Sender, Recipient, Now));
        Assert.Equal("quote_expired", ex.Code);
    }

    [Fact]
    public void Happy_path_records_every_step()
    {
        var transfer = Transfer.Create(SampleQuote(), Sender, Recipient, Now);

        transfer.MoveTo(TransferStatus.Funded, Now.AddMinutes(1), "cash at branch 12");
        transfer.MoveTo(TransferStatus.SentToPartner, Now.AddMinutes(2));
        transfer.MoveTo(TransferStatus.Paid, Now.AddHours(1), "partner ref 99812");

        Assert.Equal(TransferStatus.Paid, transfer.Status);
        Assert.Equal(3, transfer.History.Count);
        Assert.Equal("partner ref 99812", transfer.History[^1].Note);
    }

    [Theory]
    [InlineData(TransferStatus.Paid)]
    [InlineData(TransferStatus.SentToPartner)]
    [InlineData(TransferStatus.Failed)]
    public void Cannot_skip_funding(TransferStatus next)
    {
        var transfer = Transfer.Create(SampleQuote(), Sender, Recipient, Now);

        var ex = Assert.Throws<TransferRuleException>(() => transfer.MoveTo(next, Now));
        Assert.Equal("invalid_transition", ex.Code);
    }

    [Fact]
    public void Paid_transfer_cannot_be_cancelled()
    {
        var transfer = Transfer.Create(SampleQuote(), Sender, Recipient, Now);
        transfer.MoveTo(TransferStatus.Funded, Now);
        transfer.MoveTo(TransferStatus.SentToPartner, Now);
        transfer.MoveTo(TransferStatus.Paid, Now);

        Assert.False(transfer.CanMoveTo(TransferStatus.Cancelled));
    }
}
