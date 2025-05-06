using System.Security.Cryptography;

namespace SamFirmDownloader.Helper
{
    public sealed class Crc32Helper : HashAlgorithm
    {
        public const uint DefaultPolynomial = 0xedb88320u;
        public const uint DefaultSeed = 0xffffffffu;

        private static uint[]? defaultTable;

        private readonly uint seed;
        private readonly uint[] table;

        private uint hash;

        public override int HashSize
        {
            get => 32;
        }

        public Crc32Helper() : this(DefaultPolynomial, DefaultSeed)
        {
        }

        public Crc32Helper(uint polynomial, uint seed)
        {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("Not supported on Big Endian processors");

            table = InitializeTable(polynomial);
            this.seed = hash = seed;
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
                return defaultTable;

            var createTable = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                var entry = (uint)i;
                for (var j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = entry >> 1 ^ polynomial;
                    else
                        entry >>= 1;
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
                defaultTable = createTable;

            return createTable;
        }

        private static uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
        {
            var hash = seed;
            for (var i = start; i < start + size; i++)
                hash = hash >> 8 ^ table[buffer[i] ^ hash & 0xff];
            return hash;
        }

        private static byte[] UInt32ToBigEndianBytes(uint uint32)
        {
            var result = BitConverter.GetBytes(uint32);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            hash = CalculateHash(table, hash, array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            var hashBuffer = UInt32ToBigEndianBytes(~hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override void Initialize()
        {
            hash = seed;
        }

        public static uint Compute(byte[] buffer)
        {
            return Compute(DefaultSeed, buffer);
        }

        public static uint Compute(uint seed, byte[] buffer)
        {
            return Compute(DefaultPolynomial, seed, buffer);
        }

        public static uint Compute(uint polynomial, uint seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }
    }
}