using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace Altus.Suffūz
{
    public class DateRange : IComparable<DateRange>
    {
        public static readonly DateRange Forever = new DateRange(DateTime.MinValue, DateTime.MaxValue);

        public DateRange(System.DateTime start, System.DateTime end)
        {
            if (start > end) throw (new InvalidOperationException("Start date must be before end date"));
            Start = start;
            End = end;

            DateTimeFormatInfo dtfi = DateTimeFormatInfo.CurrentInfo;
            Calendar calendar = dtfi.Calendar;

            int hc = ((Start.Year << 24) >> 24) // low byte
                ^ Start.DayOfYear  // calendar.GetDayOfYear(Start) << 8  // 1 byte
                ^ ((End.Year << 24) >> 24) << 16 // 2 bytes
                ^ End.DayOfYear; // calendar.GetDayOfYear(End) << 24; // 1 byte
            _hc = hc;
        }

        public System.DateTime Start { get; private set; }
        public System.DateTime End { get; private set; }

        public bool Intersects(DateRange testRange)
        {
            return testRange.End.IsBetween(this.Start, this.End)
                || testRange.Start.IsBetween(this.Start, this.End)
                || (testRange.Start <= this.Start && testRange.End >= this.End);
        }

        public bool Contains(DateRange testRange)
        {
            return testRange.Start >= this.Start && testRange.End <= this.End;
        }

        public bool Contains(DateTime dateTime)
        {
            return dateTime.IsBetween(Start, End);
        }

        public bool Contains(long ticks)
        {
            return Start.Ticks <= ticks && End.Ticks >= ticks;
        }

        public DateRange Union(DateRange testRange)
        {
            System.DateTime start = this.Start < testRange.Start ? this.Start : testRange.Start;
            System.DateTime end = this.End > testRange.End ? this.End : testRange.End;
            return new DateRange(start, end);
        }

        public DateRange Intersect(DateRange testRange)
        {
            if (this.Intersects(testRange))
            {
                System.DateTime start = this.Start > testRange.Start ? this.Start : testRange.Start;
                System.DateTime end = this.End < testRange.End ? this.End : testRange.End;
                return new DateRange(start, end);
            }
            else
            {
                return null;
            }
        }

        public int CompareTo(DateRange other)
        {
            if (other == null) return 1;
            if (Start == other.Start && End == other.End) return 0;
            if (Intersects(other))
            {
                if (other.End > this.End) return 1;
                else return -1;
            }
            else
            {
                if (this.Start < other.Start) return -1;
                else return 1;
            }
        }

        public double TotalSeconds { get { return End.Subtract(Start).TotalSeconds; } }
        public double TotalDays { get { return End.Subtract(Start).TotalDays; } }
        public double TotalMilliseconds { get { return End.Subtract(Start).TotalMilliseconds; } }
        public double TotalMinutes { get { return End.Subtract(Start).TotalMinutes; } }
        public double TotalHours { get { return End.Subtract(Start).TotalHours; } }
        public TimeSpan Duration { get { return End.Subtract(Start); } }

        public override bool Equals(object obj)
        {
            DateRange other = obj as DateRange;
            if (other == null) return false;
            if (Start == other.Start && End == other.End) return true;
            else return false;
        }
        int _hc;
        public override int GetHashCode()
        {
            return _hc;
        }

        public override string ToString()
        {
            return Start.ToString() + " - " + End.ToString();
        }
    }

    public static class DateTimeEx
    {
        public static bool IsBetween(this System.DateTime dt, System.DateTime start, System.DateTime end)
        {
            return dt >= start && dt <= end;
        }
    }
}
