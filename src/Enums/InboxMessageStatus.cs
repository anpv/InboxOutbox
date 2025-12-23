namespace InboxOutbox.Enums;

public enum InboxMessageStatus : byte
{
    Pending,
    Received,
    Failed
}