using System;
using System.IO;
using System.Text;

namespace Poke1Protocol
{
	// Token: 0x02000002 RID: 2
	public static class CompressionHelper
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002048 File Offset: 0x00000248
		public static byte[] CompressBytes(byte[] input)
		{
			return CompressionHelper.ActOnBytes(input, new Func<byte[], CompressionHelper.OutputBuffer, int>(CompressionHelper.LZFCompress));
		}

		// Token: 0x06000002 RID: 2 RVA: 0x0000205C File Offset: 0x0000025C
		public static byte[] DecompressBytes(byte[] input)
		{
			return CompressionHelper.ActOnBytes(input, new Func<byte[], CompressionHelper.OutputBuffer, int>(CompressionHelper.LZFDecompress));
		}

		// Token: 0x06000003 RID: 3 RVA: 0x00002070 File Offset: 0x00000270
		private static byte[] ActOnBytes(byte[] inputBytes, Func<byte[], CompressionHelper.OutputBuffer, int> act)
		{
			int num = inputBytes.Length;
			CompressionHelper.OutputBuffer outputBuffer;
			int num2;
			do
			{
				num *= 2;
				outputBuffer = new byte[num];
				num2 = act(inputBytes, outputBuffer);
			}
			while (num2 == 0);
			byte[] array = new byte[num2];
			Buffer.BlockCopy(outputBuffer.bytes, 0, array, 0, num2);
			return array;
		}

		// Token: 0x06000004 RID: 4 RVA: 0x000020BC File Offset: 0x000002BC
		public static string CompressString(string input)
		{
			return CompressionHelper.ActOnString(input, new Func<byte[], byte[]>(CompressionHelper.CompressBytes));
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000020D0 File Offset: 0x000002D0
		public static string DecompressString(string input)
		{
			return CompressionHelper.ActOnString(input, new Func<byte[], byte[]>(CompressionHelper.DecompressBytes));
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020E4 File Offset: 0x000002E4
		private static string ActOnString(string input, Func<byte[], byte[]> act)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(input);
			byte[] bytes2 = act(bytes);
			return Encoding.Unicode.GetString(bytes2);
		}

		// Token: 0x06000007 RID: 7 RVA: 0x00002114 File Offset: 0x00000314
		public static void CompressFile(string path)
		{
			CompressionHelper.ActOnFile(path, new Func<byte[], byte[]>(CompressionHelper.CompressBytes));
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002128 File Offset: 0x00000328
		public static void DecompressFile(string path)
		{
			CompressionHelper.ActOnFile(path, new Func<byte[], byte[]>(CompressionHelper.DecompressBytes));
		}

		// Token: 0x06000009 RID: 9 RVA: 0x0000213C File Offset: 0x0000033C
		private static void ActOnFile(string path, Func<byte[], byte[]> act)
		{
			byte[] arg = File.ReadAllBytes(path);
			byte[] bytes = act(arg);
			File.WriteAllBytes(path, bytes);
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002164 File Offset: 0x00000364
		public static int LZFCompress(byte[] input, CompressionHelper.OutputBuffer buffer)
		{
			byte[] bytes = buffer.bytes;
			int num = input.Length;
			int num2 = bytes.Length;
			Array.Clear(CompressionHelper.HashTable, 0, (int)CompressionHelper.HSIZE);
			uint num3 = 0u;
			uint num4 = 0u;
			uint num5 = (uint)((int)input[(int)num3] << 8 | (int)input[(int)(num3 + 1u)]);
			int num6 = 0;
			for (;;)
			{
				if ((ulong)num3 < (ulong)((long)(num - 2)))
				{
					num5 = (num5 << 8 | (uint)input[(int)(num3 + 2u)]);
					long num7 = (long)((ulong)((num5 ^ num5 << 5) >> (int)(24u - CompressionHelper.HLOG - num5 * 5u) & CompressionHelper.HSIZE - 1u));
					long num8 = CompressionHelper.HashTable[(int)(checked((IntPtr)num7))];
					CompressionHelper.HashTable[(int)(checked((IntPtr)num7))] = (long)((ulong)num3);
					long num9;
					if ((num9 = (long)((ulong)num3 - (ulong)num8 - 1UL)) < (long)((ulong)CompressionHelper.MAX_OFF) && (ulong)(num3 + 4u) < (ulong)((long)num) && num8 > 0L && input[(int)(checked((IntPtr)num8))] == input[(int)num3] && input[(int)(checked((IntPtr)(unchecked(num8 + 1L))))] == input[(int)(num3 + 1u)] && input[(int)(checked((IntPtr)(unchecked(num8 + 2L))))] == input[(int)(num3 + 2u)])
					{
						uint num10 = 2u;
						uint num11 = (uint)(num - (int)num3 - (int)num10);
						num11 = ((num11 > CompressionHelper.MAX_REF) ? CompressionHelper.MAX_REF : num11);
						if ((ulong)num4 + (ulong)((long)num6) + 1UL + 3UL >= (ulong)((long)num2))
						{
							break;
						}
						do
						{
							num10 += 1u;
						}
						while (num10 < num11 && input[(int)(checked((IntPtr)(unchecked(num8 + (long)((ulong)num10)))))] == input[(int)(num3 + num10)]);
						if (num6 != 0)
						{
							bytes[(int)num4++] = (byte)(num6 - 1);
							num6 = -num6;
							do
							{
								bytes[(int)num4++] = input[(int)(checked((IntPtr)(unchecked((ulong)num3 + (ulong)((long)num6)))))];
							}
							while (++num6 != 0);
						}
						num10 -= 2u;
						num3 += 1u;
						if (num10 < 7u)
						{
							bytes[(int)num4++] = (byte)((num9 >> 8) + (long)((ulong)((ulong)num10 << 5)));
						}
						else
						{
							bytes[(int)num4++] = (byte)((num9 >> 8) + 224L);
							bytes[(int)num4++] = (byte)(num10 - 7u);
						}
						bytes[(int)num4++] = (byte)num9;
						num3 += num10 - 1u;
						num5 = (uint)((int)input[(int)num3] << 8 | (int)input[(int)(num3 + 1u)]);
						num5 = (num5 << 8 | (uint)input[(int)(num3 + 2u)]);
						CompressionHelper.HashTable[(int)((num5 ^ num5 << 5) >> (int)(24u - CompressionHelper.HLOG - num5 * 5u) & CompressionHelper.HSIZE - 1u)] = (long)((ulong)num3);
						num3 += 1u;
						num5 = (num5 << 8 | (uint)input[(int)(num3 + 2u)]);
						CompressionHelper.HashTable[(int)((num5 ^ num5 << 5) >> (int)(24u - CompressionHelper.HLOG - num5 * 5u) & CompressionHelper.HSIZE - 1u)] = (long)((ulong)num3);
						num3 += 1u;
						continue;
					}
				}
				else if ((ulong)num3 == (ulong)((long)num))
				{
					goto IL_2BD;
				}
				num6++;
				num3 += 1u;
				if ((long)num6 == (long)((ulong)CompressionHelper.MAX_LIT))
				{
					if ((ulong)(num4 + 1u + CompressionHelper.MAX_LIT) >= (ulong)((long)num2))
					{
						return 0;
					}
					bytes[(int)num4++] = (byte)(CompressionHelper.MAX_LIT - 1u);
					num6 = -num6;
					do
					{
						bytes[(int)num4++] = input[(int)(checked((IntPtr)(unchecked((ulong)num3 + (ulong)((long)num6)))))];
					}
					while (++num6 != 0);
				}
			}
			return 0;
			IL_2BD:
			if (num6 != 0)
			{
				if ((ulong)num4 + (ulong)((long)num6) + 1UL >= (ulong)((long)num2))
				{
					return 0;
				}
				bytes[(int)num4++] = (byte)(num6 - 1);
				num6 = -num6;
				do
				{
					bytes[(int)num4++] = input[(int)(checked((IntPtr)(unchecked((ulong)num3 + (ulong)((long)num6)))))];
				}
				while (++num6 != 0);
			}
			return (int)num4;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x0000247C File Offset: 0x0000067C
		public static int LZFDecompress(byte[] input, CompressionHelper.OutputBuffer buffer)
		{
			byte[] bytes = buffer.bytes;
			int num = input.Length;
			int num2 = bytes.Length;
			uint num3 = 0u;
			uint num4 = 0u;
			for (;;)
			{
				uint num5 = (uint)input[(int)num3++];
				if (num5 < 32u)
				{
					num5 += 1u;
					if ((ulong)(num4 + num5) > (ulong)((long)num2))
					{
						break;
					}
					do
					{
						bytes[(int)num4++] = input[(int)num3++];
					}
					while ((num5 -= 1u) != 0u);
				}
				else
				{
					uint num6 = num5 >> 5;
					int num7 = (int)(num4 - ((num5 & 31u) << 8) - 1u);
					if (num6 == 7u)
					{
						num6 += (uint)input[(int)num3++];
					}
					num7 -= (int)input[(int)num3++];
					if ((ulong)(num4 + num6 + 2u) > (ulong)((long)num2))
					{
						return 0;
					}
					if (num7 < 0)
					{
						return 0;
					}
					bytes[(int)num4++] = bytes[num7++];
					bytes[(int)num4++] = bytes[num7++];
					do
					{
						bytes[(int)num4++] = bytes[num7++];
					}
					while ((num6 -= 1u) != 0u);
				}
				if ((ulong)num3 >= (ulong)((long)num))
				{
					return (int)num4;
				}
			}
			return 0;
		}

		// Token: 0x04000001 RID: 1
		private static readonly uint HLOG = 14u;

		// Token: 0x04000002 RID: 2
		private static readonly uint HSIZE = 16384u;

		// Token: 0x04000003 RID: 3
		private static readonly uint MAX_LIT = 32u;

		// Token: 0x04000004 RID: 4
		private static readonly uint MAX_OFF = 8192u;

		// Token: 0x04000005 RID: 5
		private static readonly uint MAX_REF = 264u;

		// Token: 0x04000006 RID: 6
		private static readonly long[] HashTable = new long[CompressionHelper.HSIZE];

		// Token: 0x020000A9 RID: 169
		public struct OutputBuffer
		{
			// Token: 0x170000FB RID: 251
			// (get) Token: 0x060004F8 RID: 1272 RVA: 0x0001A720 File Offset: 0x00018920
			public int Length
			{
				get
				{
					return this.bytes.Length;
				}
			}

			// Token: 0x060004F9 RID: 1273 RVA: 0x0001A72C File Offset: 0x0001892C
			public static implicit operator CompressionHelper.OutputBuffer(byte[] input)
			{
				return new CompressionHelper.OutputBuffer
				{
					bytes = input
				};
			}

			// Token: 0x0400026F RID: 623
			public byte[] bytes;
		}
	}
}
