using System.Collections;

namespace InboxOutbox.EntityFrameworkCore;

public sealed record MonthlyRange(int FromYear, int FromMonth, int NumberOfMonths) : IEnumerable<DateTime>
{
    public IEnumerator<DateTime> GetEnumerator()
    {
        for (var i = 0; i < NumberOfMonths; i++)
        {
            yield return new DateTime(FromYear, FromMonth, 1).AddMonths(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}