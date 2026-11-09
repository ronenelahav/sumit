namespace Sumit.TransactionExporter.Application;

public enum ExtractionStage
{
    Ready,
    OpeningBrowser,
    WaitingForAuthentication,
    Authenticated,
    RetrievingTransactions,
    ProcessingTransactions,
    ReadyForExport,
    Completed,
    Failed,
    Cancelled,
}
