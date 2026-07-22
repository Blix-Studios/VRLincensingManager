using System;

namespace VRLicensing
{
    /// <summary>
    /// Minimal, self-contained QR Code encoder (byte mode, error-correction level M,
    /// versions 1-4 → up to 62 bytes of payload).
    ///
    /// This exists so the package can render a purchase QR **without** requiring the host
    /// project to ship ZXing. <see cref="LicenseQRScanner"/> still uses ZXing for *decoding*
    /// (which is a much harder problem); encoding a short, known URL is simple enough to own.
    ///
    /// Deliberately has no UnityEngine dependency so it can be unit-tested standalone.
    /// </summary>
    public static class QRCodeEncoder
    {
        // Per-version tables for ECC level M. Index = version - 1.
        private static readonly int[] TotalCodewords = { 26, 44, 70, 100 };
        private static readonly int[] DataCodewords = { 16, 28, 44, 64 };
        private static readonly int[] EcCodewordsPerBlock = { 10, 16, 26, 18 };
        private static readonly int[] BlockCount = { 1, 1, 1, 2 };

        private const int EcLevelM = 0; // format-info bits for level M

        /// <summary>
        /// Encodes <paramref name="text"/> as a QR matrix. <c>true</c> = dark module.
        /// Indexed <c>[row, col]</c>.
        /// </summary>
        /// <param name="forceMask">Mask pattern 0-7 to force; -1 (default) picks the lowest-penalty
        /// mask per the spec. Forcing is only useful for tests.</param>
        /// <exception cref="ArgumentException">Payload does not fit in versions 1-4.</exception>
        public static bool[,] Encode(string text, int forceMask = -1)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("text is empty");

            byte[] payload = System.Text.Encoding.UTF8.GetBytes(text);

            // Pick the smallest version that fits: 4 bits mode + 8 bits length + payload.
            int version = -1;
            for (int v = 1; v <= 4; v++)
            {
                if (payload.Length + 2 <= DataCodewords[v - 1]) { version = v; break; }
            }
            if (version < 0)
                throw new ArgumentException($"Payload of {payload.Length} bytes exceeds QR version 4 (level M).");

            byte[] codewords = BuildCodewords(payload, version);
            byte[] finalData = InterleaveWithEc(codewords, version);

            int size = 17 + 4 * version;
            var modules = new bool[size, size];
            var isFunction = new bool[size, size];

            DrawFunctionPatterns(modules, isFunction, size, version);
            DrawCodewords(modules, isFunction, size, finalData);

            // Try all 8 masks, keep the one with the lowest penalty.
            // Note: implementations disagree on whether the format bits should be present
            // while scoring (ISO/IEC 18004 §7.8.3 is ambiguous). We score the complete
            // symbol, format bits included. Any of the 8 masks yields a valid, scannable
            // symbol, so this only shifts a quality heuristic by a small margin.
            int bestMask = forceMask;
            if (bestMask < 0)
            {
                bestMask = 0;
                int bestPenalty = int.MaxValue;
                for (int mask = 0; mask < 8; mask++)
                {
                    ApplyMask(modules, isFunction, size, mask);
                    DrawFormatBits(modules, isFunction, size, mask);
                    int penalty = ComputePenalty(modules, size);
                    if (penalty < bestPenalty) { bestPenalty = penalty; bestMask = mask; }
                    ApplyMask(modules, isFunction, size, mask); // XOR again to undo
                }
            }

            ApplyMask(modules, isFunction, size, bestMask);
            DrawFormatBits(modules, isFunction, size, bestMask);

            return modules;
        }

        // ─────────────────── data encoding ───────────────────

        private static byte[] BuildCodewords(byte[] payload, int version)
        {
            int capacityBytes = DataCodewords[version - 1];
            var bits = new BitBuffer(capacityBytes * 8);

            bits.Append(0b0100, 4);            // byte mode
            bits.Append(payload.Length, 8);    // char count (8 bits for versions 1-9)
            foreach (byte b in payload) bits.Append(b, 8);

            // Terminator: up to 4 zero bits, only as many as fit.
            int capacityBits = capacityBytes * 8;
            bits.Append(0, Math.Min(4, capacityBits - bits.Length));

            // Pad to a byte boundary, then alternate the standard pad codewords.
            bits.Append(0, (8 - bits.Length % 8) % 8);

            var result = new byte[capacityBytes];
            bits.CopyTo(result);

            for (int i = bits.Length / 8, alt = 0; i < capacityBytes; i++, alt++)
                result[i] = (byte)(alt % 2 == 0 ? 0xEC : 0x11);

            return result;
        }

        /// <summary>Splits into blocks, appends Reed-Solomon EC, and interleaves.</summary>
        private static byte[] InterleaveWithEc(byte[] data, int version)
        {
            int numBlocks = BlockCount[version - 1];
            int ecLen = EcCodewordsPerBlock[version - 1];
            int totalCodewords = TotalCodewords[version - 1];

            // Short blocks come first; long blocks carry one extra data codeword.
            int shortBlockDataLen = data.Length / numBlocks;
            int numLongBlocks = data.Length % numBlocks;

            var dataBlocks = new byte[numBlocks][];
            var ecBlocks = new byte[numBlocks][];
            var generator = BuildGenerator(ecLen);

            for (int i = 0, offset = 0; i < numBlocks; i++)
            {
                int len = shortBlockDataLen + (i >= numBlocks - numLongBlocks ? 1 : 0);
                var block = new byte[len];
                Array.Copy(data, offset, block, 0, len);
                offset += len;

                dataBlocks[i] = block;
                ecBlocks[i] = ReedSolomon(block, generator);
            }

            var result = new byte[totalCodewords];
            int pos = 0;

            int maxDataLen = shortBlockDataLen + (numLongBlocks > 0 ? 1 : 0);
            for (int i = 0; i < maxDataLen; i++)
                for (int b = 0; b < numBlocks; b++)
                    if (i < dataBlocks[b].Length) result[pos++] = dataBlocks[b][i];

            for (int i = 0; i < ecLen; i++)
                for (int b = 0; b < numBlocks; b++)
                    result[pos++] = ecBlocks[b][i];

            return result;
        }

        // ─────────────────── Reed-Solomon over GF(256) ───────────────────

        private static readonly byte[] GfExp = new byte[512];
        private static readonly byte[] GfLog = new byte[256];

        static QRCodeEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)x;
                GfLog[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D; // primitive polynomial
            }
            for (int i = 255; i < 512; i++) GfExp[i] = GfExp[i - 255];
        }

        private static byte GfMul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        /// <summary>Generator polynomial: product of (x - α^i) for i in [0, degree).</summary>
        private static byte[] BuildGenerator(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1; // coefficients stored with the constant term last

            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = GfMul(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMul(root, 2);
            }
            return result;
        }

        private static byte[] ReedSolomon(byte[] data, byte[] generator)
        {
            var result = new byte[generator.Length];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ result[0]);
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= GfMul(generator[i], factor);
            }
            return result;
        }

        // ─────────────────── matrix construction ───────────────────

        private static void DrawFunctionPatterns(bool[,] m, bool[,] fn, int size, int version)
        {
            // Timing patterns
            for (int i = 0; i < size; i++)
            {
                Set(m, fn, 6, i, i % 2 == 0);
                Set(m, fn, i, 6, i % 2 == 0);
            }

            // Finder patterns + separators
            DrawFinder(m, fn, size, 3, 3);
            DrawFinder(m, fn, size, 3, size - 4);
            DrawFinder(m, fn, size, size - 4, 3);

            // Alignment pattern (versions 2-4 have exactly one, at the bottom-right)
            if (version >= 2)
                DrawAlignment(m, fn, size - 7, size - 7);

            // Reserve the format-info area (written later by DrawFormatBits).
            // Index 6 is skipped: (8,6) and (6,8) belong to the timing patterns, not the
            // format area, and blanking them here would corrupt the finder alignment.
            for (int i = 0; i < 9; i++)
            {
                if (i == 6) continue;
                Set(m, fn, 8, i, false);
                Set(m, fn, i, 8, false);
            }
            for (int i = 0; i < 8; i++)
            {
                Set(m, fn, 8, size - 1 - i, false);
                Set(m, fn, size - 1 - i, 8, false);
            }
        }

        private static void DrawFinder(bool[,] m, bool[,] fn, int size, int centerRow, int centerCol)
        {
            for (int dr = -4; dr <= 4; dr++)
            {
                for (int dc = -4; dc <= 4; dc++)
                {
                    int r = centerRow + dr, c = centerCol + dc;
                    if (r < 0 || r >= size || c < 0 || c >= size) continue;
                    int dist = Math.Max(Math.Abs(dr), Math.Abs(dc));
                    Set(m, fn, r, c, dist != 2 && dist <= 3);
                }
            }
        }

        private static void DrawAlignment(bool[,] m, bool[,] fn, int centerRow, int centerCol)
        {
            for (int dr = -2; dr <= 2; dr++)
                for (int dc = -2; dc <= 2; dc++)
                    Set(m, fn, centerRow + dr, centerCol + dc,
                        Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1);
        }

        private static void Set(bool[,] m, bool[,] fn, int row, int col, bool dark)
        {
            m[row, col] = dark;
            fn[row, col] = true;
        }

        /// <summary>Zigzag placement of the codeword bitstream into the non-function modules.</summary>
        private static void DrawCodewords(bool[,] m, bool[,] fn, int size, byte[] data)
        {
            int bitIndex = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5; // skip the vertical timing column
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int col = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int row = upward ? size - 1 - vert : vert;

                        if (fn[row, col] || bitIndex >= data.Length * 8) continue;

                        m[row, col] = ((data[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                        bitIndex++;
                    }
                }
            }
        }

        private static void ApplyMask(bool[,] m, bool[,] fn, int size, int mask)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (fn[r, c]) continue;

                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (r + c) % 2 == 0; break;
                        case 1: invert = r % 2 == 0; break;
                        case 2: invert = c % 3 == 0; break;
                        case 3: invert = (r + c) % 3 == 0; break;
                        case 4: invert = (r / 2 + c / 3) % 2 == 0; break;
                        case 5: invert = r * c % 2 + r * c % 3 == 0; break;
                        case 6: invert = (r * c % 2 + r * c % 3) % 2 == 0; break;
                        case 7: invert = ((r + c) % 2 + r * c % 3) % 2 == 0; break;
                        default: throw new ArgumentException("mask");
                    }

                    if (invert) m[r, c] = !m[r, c];
                }
            }
        }

        private static void DrawFormatBits(bool[,] m, bool[,] fn, int size, int mask)
        {
            int data = EcLevelM << 3 | mask;

            // BCH(15,5) error correction
            int rem = data;
            for (int i = 0; i < 10; i++) rem = rem << 1 ^ (rem >> 9) * 0x537;
            int bits = (data << 10 | rem) ^ 0x5412;

            // Copy 1 — around the top-left finder
            for (int i = 0; i <= 5; i++) Set(m, fn, i, 8, GetBit(bits, i));
            Set(m, fn, 7, 8, GetBit(bits, 6));
            Set(m, fn, 8, 8, GetBit(bits, 7));
            Set(m, fn, 8, 7, GetBit(bits, 8));
            for (int i = 9; i < 15; i++) Set(m, fn, 8, 14 - i, GetBit(bits, i));

            // Copy 2 — split between the other two finders
            for (int i = 0; i <= 7; i++) Set(m, fn, 8, size - 1 - i, GetBit(bits, i));
            for (int i = 8; i < 15; i++) Set(m, fn, size - 15 + i, 8, GetBit(bits, i));

            Set(m, fn, size - 8, 8, true); // always-dark module
        }

        private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

        // ─────────────────── mask penalty scoring ───────────────────

        /// <summary>Mask penalty per ISO/IEC 18004 rules 1-4. Internal so tests can verify it.</summary>
        internal static int ComputePenalty(bool[,] m, int size)
        {
            int penalty = 0;

            // Rule 1: runs of 5+ same-coloured modules in a row/column.
            for (int i = 0; i < size; i++)
            {
                penalty += RunPenalty(m, size, i, true);
                penalty += RunPenalty(m, size, i, false);
            }

            // Rule 2: 2x2 blocks of the same colour.
            for (int r = 0; r < size - 1; r++)
            {
                for (int c = 0; c < size - 1; c++)
                {
                    bool v = m[r, c];
                    if (v == m[r, c + 1] && v == m[r + 1, c] && v == m[r + 1, c + 1])
                        penalty += 3;
                }
            }

            // Rule 3: finder-like patterns (1:1:3:1:1 with a 4-module light gap).
            var line = new bool[size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++) line[c] = m[r, c];
                penalty += FinderLikePenalty(line);
            }
            for (int c = 0; c < size; c++)
            {
                for (int r = 0; r < size; r++) line[r] = m[r, c];
                penalty += FinderLikePenalty(line);
            }

            // Rule 4: deviation from a 50% dark ratio, in 5% steps.
            int dark = 0;
            foreach (bool b in m) if (b) dark++;
            int total = size * size;
            penalty += 10 * (Math.Abs(100 * dark - 50 * total) / (5 * total));

            return penalty;
        }

        private static int RunPenalty(bool[,] m, int size, int line, bool horizontal)
        {
            int penalty = 0, runLength = 1;
            bool runColor = horizontal ? m[line, 0] : m[0, line];

            for (int i = 1; i < size; i++)
            {
                bool v = horizontal ? m[line, i] : m[i, line];
                if (v == runColor)
                {
                    runLength++;
                    if (runLength == 5) penalty += 3;
                    else if (runLength > 5) penalty++;
                }
                else
                {
                    runColor = v;
                    runLength = 1;
                }
            }
            return penalty;
        }

        private static readonly bool[] FinderLike =
            { true, false, true, true, true, false, true };

        /// <summary>
        /// Scores one row/column for rule 3: each occurrence of the 1:1:3:1:1 finder-like
        /// sequence that is preceded or followed by four light modules costs 40 points.
        /// Modules outside the symbol count as light (the quiet zone), so patterns flush
        /// against an edge still score.
        /// </summary>
        private static int FinderLikePenalty(bool[] line)
        {
            int n = line.Length;
            int score = 0;
            int idx = 0;

            while (idx <= n - 7)
            {
                bool core = true;
                for (int i = 0; i < 7; i++)
                {
                    if (line[idx + i] != FinderLike[i]) { core = false; break; }
                }
                if (!core) { idx++; continue; }

                bool lightBefore = true;
                for (int i = Math.Max(0, idx - 4); i < idx; i++)
                    if (line[i]) { lightBefore = false; break; }

                bool lightAfter = true;
                for (int i = idx + 7; i < Math.Min(n, idx + 11); i++)
                    if (line[i]) { lightAfter = false; break; }

                if (lightBefore || lightAfter)
                {
                    score += 40;
                    idx += 7;
                }
                else
                {
                    // Overlapping match: the next candidate can start at the middle
                    // dark run, so resume from there rather than skipping the whole core.
                    idx += 4;
                }
            }

            return score;
        }

        // ─────────────────── bit buffer ───────────────────

        private sealed class BitBuffer
        {
            private readonly byte[] bytes;
            public int Length { get; private set; }

            public BitBuffer(int capacityBits) => bytes = new byte[(capacityBits + 7) / 8];

            public void Append(int value, int bitCount)
            {
                for (int i = bitCount - 1; i >= 0; i--)
                {
                    if (((value >> i) & 1) != 0)
                        bytes[Length >> 3] |= (byte)(1 << (7 - (Length & 7)));
                    Length++;
                }
            }

            public void CopyTo(byte[] destination) =>
                Array.Copy(bytes, destination, Math.Min(bytes.Length, destination.Length));
        }
    }
}
