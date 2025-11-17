namespace InboxOutbox.Entities;

public class Measurement
{
    public long Id { get; set; }

    public int Value { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}