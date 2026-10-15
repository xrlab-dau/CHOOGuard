using System;

namespace ChooGuard.Contracts
{
    /// <summary>표시명과 무관한 ASCII 식별자. default 값은 유효 ID가 아니다.</summary>
    public readonly struct StableId : IEquatable<StableId>
    {
        private readonly string value;
        public string Value => value ?? throw new InvalidOperationException("초기화되지 않은 ID입니다.");
        public bool IsValid => value != null;
        public StableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || !IsAlphaNumeric(value[0]))
                throw new ArgumentException("ID는 ASCII 영숫자로 시작하는 1~128자여야 합니다.", nameof(value));
            foreach (var c in value)
                if (!IsAlphaNumeric(c) && c != '.' && c != '_' && c != ':' && c != '-')
                    throw new ArgumentException("ID에 허용되지 않은 문자가 있습니다.", nameof(value));
            this.value = value;
        }
        private static bool IsAlphaNumeric(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        public bool Equals(StableId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is StableId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => Value;
    }

    public sealed class NamedIdentity
    {
        public StableId Id { get; }
        public string DisplayName { get; }
        public NamedIdentity(StableId id, string displayName)
        {
            Id = ContractGuard.Id(id, nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }
    }

    /// <summary>64bit 비음수 simulation microseconds. Sequence나 wall clock으로 암묵 변환하지 않는다.</summary>
    public readonly struct SimTick
    {
        public long Microseconds { get; }
        public SimTick(long microseconds) { Microseconds = ContractGuard.Nonnegative(microseconds, nameof(microseconds)); }
    }

    /// <summary>계측용 monotonic microseconds. simulation tick 및 UTC 시각과 별개다.</summary>
    public readonly struct MonotonicTimestamp
    {
        public long Microseconds { get; }
        public MonotonicTimestamp(long microseconds) { Microseconds = ContractGuard.Nonnegative(microseconds, nameof(microseconds)); }
    }

    /// <summary>사건 순서이며 시간이 아니다.</summary>
    public readonly struct Sequence
    {
        public long Value { get; }
        public Sequence(long value) { Value = ContractGuard.Nonnegative(value, nameof(value)); }
    }

    /// <summary>UTC wall clock. authoredAt/observedAt/receivedAt의 서로 다른 필드에 저장한다.</summary>
    public readonly struct UtcTimestamp
    {
        public DateTimeOffset Value { get; }
        public UtcTimestamp(DateTimeOffset value)
        {
            if (value.Offset != TimeSpan.Zero) throw new ArgumentException("UTC 시각만 허용합니다.", nameof(value));
            Value = value;
        }
        public UtcTimestamp(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("명시적인 UTC 시각이 필요합니다.", nameof(value));
            Value = new DateTimeOffset(value);
        }
        public string ToRfc3339() => Value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    public enum SiUnit { Metre, SquareMetre, CubicMetre, Second, Kilogram, MetresPerSecond, Kelvin, Pascal }

    /// <summary>SI 단위와 유한 수치를 함께 보관한다. 음수 허용 여부는 해당 물리량의 도메인 규칙이다.</summary>
    public readonly struct SiValue
    {
        public double Value { get; }
        public SiUnit Unit { get; }
        public SiValue(double value, SiUnit unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            ContractGuard.Defined(unit, nameof(unit));
            Value = value; Unit = unit;
        }
    }

    internal static class ContractGuard
    {
        internal static StableId Id(StableId value, string name)
        {
            if (!value.IsValid) throw new ArgumentException("초기화된 ID가 필요합니다.", name);
            return value;
        }
        internal static long Nonnegative(long value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
        internal static T NotNull<T>(T value, string name) where T : class => value ?? throw new ArgumentNullException(name);
        internal static T Defined<T>(T value, string name) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }
}
