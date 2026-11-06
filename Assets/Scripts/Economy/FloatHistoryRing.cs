namespace BuildATower
{
    /// <summary>
    /// Fixed-capacity ring of daily float totals (e.g. wait-minute sums).
    /// Average is over recorded days only (no leading-zero padding).
    /// </summary>
    public sealed class FloatHistoryRing
    {
        public const int Capacity = VisitHistoryRing.Capacity;

        readonly float[] _days = new float[Capacity];
        int _count;
        int _next;

        public int RecordedDays => _count;

        public float Yesterday =>
            _count == 0 ? 0f : _days[(_next - 1 + Capacity) % Capacity];

        public void Push(float value)
        {
            if (value < 0f) value = 0f;
            _days[_next] = value;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public float Sum()
        {
            var sum = 0f;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_next - _count + i + Capacity) % Capacity;
                sum += _days[idx];
            }

            return sum;
        }

        public float Average()
        {
            if (_count <= 0) return 0f;
            return Sum() / _count;
        }
    }
}
