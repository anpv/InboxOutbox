namespace InboxOutbox.Enums;

public enum OutboxMessageStatus : byte
{
    Pending,
    Sending,
    Sent
}