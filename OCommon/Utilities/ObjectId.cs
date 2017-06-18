using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    /// <summary>
    /// ID生成
    /// </summary>
    public class ObjectId : IComparable<ObjectId>, IEquatable<ObjectId>
    {
        private static readonly DateTime __unixEpoch;
        private static readonly long __dateTimeMaxValueMillisecondsSinceEpoch;
        private static readonly long __dateTimeMinValueMillisecondsSinceEpoch;
        private static ObjectId __emptyInstance = default(ObjectId);
        private static int __staticMachine;
        private static short __staticPid;
        private static int __staticIncrement;
        private static uint[] _lookup32 = Enumerable.Range(0, 256).Select(i =>
        {
            string s = i.ToString("X2");
            return ((uint)s[0]) + ((uint)s[1] << 16);
        }).ToArray();

        private int _timestamp;
        private int _machine;
        private short _pid;
        private int _increment;
        private int machine;
        private short pid;
        private int increment;

        static ObjectId()
        {
            __unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            __dateTimeMaxValueMillisecondsSinceEpoch = (DateTime.MaxValue - __unixEpoch).Ticks;
            __dateTimeMinValueMillisecondsSinceEpoch = (DateTime.MinValue - __unixEpoch).Ticks;
            __staticMachine = GetMachineHash();
            __staticIncrement = (new Random()).Next();
            __staticPid = (short)GetCurrentProcessId();
        }
        public ObjectId(byte[] bytes)
        {
            Check.NotNull(bytes, nameof(bytes));

            UnPack(bytes, out _timestamp, out _machine, out _pid, out _increment);
        }
        public ObjectId(DateTime timestamp,int machine,short pid,int increment):
            this(GetTimestampFromDateTime(timestamp), machine, pid, increment)
        {

        }

        public static int GetTimestampFromDateTime(DateTime timestamp)
        {
            return (int)Math.Floor((ToUniversalTime(timestamp) - __unixEpoch).TotalSeconds);
        }

        public static DateTime ToUniversalTime(DateTime timestamp)
        {
            if (timestamp == DateTime.MinValue)
                return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            else if (timestamp == DateTime.MaxValue)
                return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            else
                return timestamp.ToUniversalTime();
        }

        public ObjectId(int timestamp,int machine,short pid,int increment)
        {
            if ((machine & 0xff000000) != 0)
                throw new ArgumentOutOfRangeException("machine", "machine值必须在0到16777215之间（3字节）");

            if ((increment & 0xff000000) != 0)
                throw new ArgumentOutOfRangeException("increment", "increment值必须在0到16777215之间（3字节）");

            _timestamp = timestamp;
            _machine = machine;
            _pid = pid;
            _increment = increment;
        }
        public ObjectId(string value)
        {
            Check.NotNull(value, nameof(value));

            UnPack(ParseHexString(value), out _timestamp, out _machine, out _pid, out _increment);
        }

        public ObjectId(byte[] bytes, int machine, short pid, int increment) : this(bytes)
        {
            this.machine = machine;
            this.pid = pid;
            this.increment = increment;
        }

        /// <summary>
        /// 空对象
        /// </summary>
        public static ObjectId Empty=> __emptyInstance; 
        /// <summary>
        /// 时间戳
        /// </summary>
        public int Timestamp => _timestamp;
        public int Machine => _machine;
        public short Pid => _pid;
        public int Increment => _increment;
        /// <summary>
        /// 获取创建时间（根据时间戳计算获取）
        /// </summary>
        public DateTime CreationTime => __unixEpoch.AddSeconds(_timestamp);

       public static bool operator <(ObjectId lhs,ObjectId rhs)
        {
            return lhs.CompareTo(rhs) < 0;
        }
        public static bool operator >(ObjectId lhs, ObjectId rhs)
        {
            return lhs.CompareTo(rhs) > 0;
        }
        public static bool operator <=(ObjectId lhs, ObjectId rhs)
        {
            return lhs.CompareTo(rhs) <= 0;
        }
        public static bool operator >=(ObjectId lhs, ObjectId rhs)
        {
            return lhs.CompareTo(rhs) >= 0;
        }
        public static bool operator ==(ObjectId lhs, ObjectId rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(ObjectId lhs, ObjectId rhs)
        {
            return !lhs.Equals(rhs);
        }
        public static ObjectId GenerateNewId()
        {
            return GenerateNewId(GetTimestampFromDateTime(DateTime.UtcNow));
        }

        private static ObjectId GenerateNewId(int timestamp)
        {
            int increment = Interlocked.Increment(ref __staticIncrement) & 0x00ffffff;
            return new ObjectId(timestamp, __staticMachine, __staticPid, increment);
        }

        private static ObjectId GenerateNewId(DateTime timestamp)
        {
            return GenerateNewId(GetTimestampFromDateTime(timestamp));
        }
        public static string GenerateNewStringId()
        {
            return GenerateNewId().ToString();
        }
        public static byte[] Pack(int timestamp,int machine,short pid,int increment)
        {
            if ((machine & 0xff000000) != 0)
                throw new ArgumentOutOfRangeException("machine", "machine值必须在0到16777215之间（3字节）");

            if ((increment & 0xff000000) != 0)
                throw new ArgumentOutOfRangeException("increment", "increment值必须在0到16777215之间（3字节）");

            byte[] bytes = new byte[12];
            bytes[0] = (byte)(timestamp >> 24);
            bytes[1]= (byte)(timestamp >> 16);
            bytes[2] = (byte)(timestamp >> 8);
            bytes[3] = (byte)(timestamp);
            bytes[4] = (byte)(machine >> 16);
            bytes[5] = (byte)(machine >> 8);
            bytes[6] = (byte)(machine);
            bytes[7] = (byte)(pid >> 8);
            bytes[8] = (byte)(pid);
            bytes[9] = (byte)(increment >> 16);
            bytes[10] = (byte)(increment >> 8);
            bytes[11] = (byte)(increment);
            return bytes;
        }
        public static ObjectId Parse(string s)
        {
            Check.NotNull(s, nameof(s));

            if (s.Length != 24)
                throw new ArgumentOutOfRangeException("s", "ObjectId字符串长度必须为24位");

            return new ObjectId(ParseHexString(s));
        }
        public static byte[] ParseHexString(string value)
        {
            Check.NotNull(value, nameof(value));
            if (value.Length % 2 == 1)
                throw new ArgumentException("value的长度必须为偶数");

            int length = value.Length >> 1;
            byte[] arr = new byte[length];
            //使用移位将字符转换为16进制数
            for(int i = 0; i < length; ++i)
            {
                arr[i] = (byte)((GetHaxVal(value[i << 1]) << 4) + (GetHaxVal(value[(i << 1) + 1])));
            }
            return arr;
        }

        private static int GetHaxVal(char hex)
        {
            int value = (int)hex;
            //若字符为大写A-F
            //则将返回value-(val<58?48:55)
            //若字符为小写a-f
            //则将返回value - (val < 48 ? 48 : 87)
            //具体参考ASCII
            return value - (value < 58 ? 48 : (value < 97 ? 55 : 87));
        }

        public static void UnPack(byte[] bytes, out int timestamp, out int machine, out short pid, out int increment)
        {
            Check.NotNull(bytes, nameof(bytes));

            if (bytes.Length != 12)
                throw new ArgumentOutOfRangeException("bytes", "数组长度必须为12");

            timestamp = (bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3];
            machine = (bytes[4] << 16) + (bytes[5] << 8) + bytes[6];
            pid = (short)((bytes[7] << 8) + bytes[8]);
            increment = (bytes[9] << 16) + (bytes[10] << 8) + bytes[11];
        }

        private static int GetMachineHash()
        {
            var hostName = Environment.MachineName;
            var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(hostName));
            return (hash[0] << 16) + (hash[1] << 8) + hash[2];//使用3字节哈希值
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetCurrentProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }
        public int CompareTo(ObjectId other)
        {
            int r = _timestamp.CompareTo(other._timestamp);
            if (r != 0) return r;
            r = _machine.CompareTo(other._machine);
            if (r != 0) return r;
            r = _pid.CompareTo(other._pid);
            if (r != 0) return r;
            return _increment.CompareTo(other._increment);
        }

        public bool Equals(ObjectId other)
        {
            return _timestamp == other._timestamp &&
                _machine == other._machine &&
                _pid == other._pid &&
                _increment == other._increment;
        }
        public override bool Equals(object obj)
        {
            if (obj is ObjectId)
                return Equals((ObjectId)obj);
            return false;
        }
        public byte[] ToByteArray()
        {
            return Pack(_timestamp, _machine, _pid, _increment);
        }
        public override string ToString()
        {
            return ToHexString(ToByteArray());
        }

        public static string ToHexString(byte[] bytes)
        {
            Check.NotNull(bytes, nameof(bytes));

            var result = new char[bytes.Length * 2];
            for(int i = 0; i < bytes.Length; i++)
            {
                var val = _lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }
        public static long ToMillisecondsSinceEpoch(DateTime dateTime)
        {
            var utcDateTime = ToUniversalTime(dateTime);
            return (utcDateTime - __unixEpoch).Ticks / 10000;
        }
        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + _timestamp.GetHashCode();
            hash = 37 * hash + _machine.GetHashCode();
            hash = 37 * hash + pid.GetHashCode();
            hash = 37 * hash + _increment.GetHashCode();
            return hash;
        }
    }
}
