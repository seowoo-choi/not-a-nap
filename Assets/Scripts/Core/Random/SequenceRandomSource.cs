using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 테스트용 난수 소스: 지정한 값 시퀀스를 순서대로 반환한다.
    /// loop=true(기본)면 끝에서 처음으로 되돌아가고, false면 소진 시 예외를 던진다.
    /// </summary>
    public sealed class SequenceRandomSource : IRandomSource
    {
        private readonly IReadOnlyList<double> _values;
        private readonly bool _loop;
        private int _index;

        public SequenceRandomSource(IReadOnlyList<double> values, bool loop = true)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("시퀀스는 비어 있을 수 없다.", nameof(values));
            _values = values;
            _loop = loop;
        }

        public SequenceRandomSource(params double[] values) : this((IReadOnlyList<double>)values) { }

        /// <summary>지금까지 소비된 난수 개수.</summary>
        public int Consumed { get; private set; }

        public double NextDouble()
        {
            if (_index >= _values.Count)
            {
                if (!_loop) throw new InvalidOperationException("SequenceRandomSource가 소진되었다.");
                _index = 0;
            }
            Consumed++;
            return _values[_index++];
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            int v = (int)(NextDouble() * maxExclusive);
            return v >= maxExclusive ? maxExclusive - 1 : v;
        }
    }
}
