namespace InboxOutbox.EntityFrameworkCore;

public static class ExtNpgsqlAnnotationNames
{
    public const string BasePrefix = "Ext:Npgsql:";

    public static class PartitionByRange
    {
        private const string Prefix = BasePrefix + "PartitionByRange:";

        public const string Key = Prefix + "Key";
    }
}