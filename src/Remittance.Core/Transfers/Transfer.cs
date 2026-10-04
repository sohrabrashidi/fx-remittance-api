using Remittance.Core.Quotes;

namespace Remittance.Core.Transfers;

public sealed record Party(string FullName, string Country, string? AccountNumber = null);

public sealed record TransferEvent(TransferStatus From, TransferStatus To, DateTimeOffset At, string? Note);

public sealed class Transfer
{
    private static readonly Dictionary<TransferStatus, TransferStatus[]> AllowedTransitions = new()
    {
        [TransferStatus.Created] = [TransferStatus.Funded, TransferStatus.Cancelled],
        [TransferStatus.Funded] = [TransferStatus.SentToPartner, TransferStatus.Cancelled],
        [TransferStatus.SentToPartner] = [TransferStatus.Paid, TransferStatus.Failed],
        [TransferStatus.Paid] = [],
        [TransferStatus.Cancelled] = [],
        [TransferStatus.Failed] = [],
    };

    private readonly List<TransferEvent> _history = [];

    private Transfer(Guid id, Quote quote, Party sender, Party recipient, string reference, DateTimeOffset createdAt)
    {
        Id = id;
        Quote = quote;
        Sender = sender;
        Recipient = recipient;
        Reference = reference;
        CreatedAt = createdAt;
        Status = TransferStatus.Created;
    }

    public Guid Id { get; }

    public Quote Quote { get; }

    public Party Sender { get; }

    public Party Recipient { get; }

    /// <summary>Short human-readable reference printed on the customer receipt.</summary>
    public string Reference { get; }

    public DateTimeOffset CreatedAt { get; }

    public TransferStatus Status { get; private set; }

    public IReadOnlyList<TransferEvent> History => _history;

    public static Transfer Create(Quote quote, Party sender, Party recipient, DateTimeOffset now)
    {
        if (quote.IsExpired(now))
        {
            throw new TransferRuleException("quote_expired", "The quote has expired. Request a new one.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sender.FullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient.FullName);

        var id = Guid.NewGuid();
        return new Transfer(id, quote, sender, recipient, MakeReference(id, now), now);
    }

    /// <summary>Rebuilds a transfer from storage without re-running creation rules.</summary>
    public static Transfer Restore(
        Guid id,
        Quote quote,
        Party sender,
        Party recipient,
        string reference,
        DateTimeOffset createdAt,
        TransferStatus status,
        IEnumerable<TransferEvent> history)
    {
        var transfer = new Transfer(id, quote, sender, recipient, reference, createdAt) { Status = status };
        transfer._history.AddRange(history);
        return transfer;
    }

    public bool CanMoveTo(TransferStatus next) => AllowedTransitions[Status].Contains(next);

    public void MoveTo(TransferStatus next, DateTimeOffset at, string? note = null)
    {
        if (!CanMoveTo(next))
        {
            throw new TransferRuleException(
                "invalid_transition",
                $"Transfer {Reference} cannot go from {Status} to {next}.");
        }

        _history.Add(new TransferEvent(Status, next, at, note));
        Status = next;
    }

    private static string MakeReference(Guid id, DateTimeOffset now) =>
        $"TR{now:yyMMdd}-{id.ToString("N")[..6].ToUpperInvariant()}";
}

public sealed class TransferRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
