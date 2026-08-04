namespace BuildATower
{
    /// <summary>
    /// Fixed-capacity ring of daily visit counts (newest at the write head).
    /// Average is over recorded days only (no leading-zero padding).
    /// </summary>
    public sealed class VisitHistoryRing
    {
        public const int Capacity = 7;

        readonly int[] _days = new int[Capacity];
        int _count;
        int _next;

        public int RecordedDays => _count;

        public int Yesterday =>
            _count == 0 ? 0 : _days[(_next - 1 + Capacity) % Capacity];

        public void Push(int visits)
        {
            if (visits < 0) visits = 0;
            _days[_next] = visits;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public float Average()
        {
            if (_count <= 0) return 0f;
            var sum = 0;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_next - _count + i + Capacity) % Capacity;
                sum += _days[idx];
            }

            return sum / (float)_count;
        }

        public int Sum()
        {
            var sum = 0;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_next - _count + i + Capacity) % Capacity;
                sum += _days[idx];
            }

            return sum;
        }
    }
}
