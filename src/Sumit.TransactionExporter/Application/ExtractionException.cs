namespace Sumit.TransactionExporter.Application;

/// <summary>
/// An expected extraction failure. The message is shown to the user, so it must never contain
/// credentials, page content or transaction values.
/// </summary>
public sealed class ExtractionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
