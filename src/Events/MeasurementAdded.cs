namespace InboxOutbox.Events;

public sealed record MeasurementAdded(long Id, int Value);