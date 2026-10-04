namespace Remittance.Core.Transfers;

public enum TransferStatus
{
    Created,
    Funded,
    SentToPartner,
    Paid,
    Cancelled,
    Failed,
}
