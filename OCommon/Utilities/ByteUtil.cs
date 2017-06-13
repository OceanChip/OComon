using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public class ByteUtil
    {
        public static readonly byte[] ZeroLengthBytes = BitConverter.GetBytes(0);
        public static readonly byte[] EmptyBytes = new byte[0];
        /// <summary>
        /// 编码字符串(utf-8)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="lengthBytes"></param>
        /// <param name="dataBytes"></param>
        public static void EncodeString(string data,out byte[] lengthBytes,out byte[] dataBytes)
        {
            if(data != null)
            {
                dataBytes = Encoding.UTF8.GetBytes(data);
                lengthBytes = BitConverter.GetBytes(dataBytes.Length);
            }
            else
            {
                dataBytes = EmptyBytes;
                lengthBytes = ZeroLengthBytes;
            }
        }
        /// <summary>
        /// 编码时间
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] EncodeDateTime(DateTime data)
        {
            return BitConverter.GetBytes(data.Ticks);
        }
        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="sourceBuffer"></param>
        /// <param name="startOffset"></param>
        /// <param name="nextStartOffset"></param>
        /// <returns></returns>
        public static string DecodeString(byte[] sourceBuffer,int startOffset,out int nextStartOffset)
        {
            return Encoding.UTF8.GetString(DecodeBytes(sourceBuffer, startOffset, out nextStartOffset));
        }
        /// <summary>
        /// 获取16位整型 （偏移量+2）
        /// </summary>
        /// <param name="sourceBuffer"></param>
        /// <param name="startOffset"></param>
        /// <param name="nextStartOffset"></param>
        /// <returns></returns>
        public static short DecodeShort(byte[] sourceBuffer, int startOffset, out int nextStartOffset)
        {
            //var shortBytes = new byte[2];
            //Buffer.BlockCopy(sourceBuffer, startOffset, shortBytes, 0, 2);
            nextStartOffset = startOffset + 2;
            return BitConverter.ToInt16(sourceBuffer, startOffset);
        }
        /// <summary>
        /// 获取32位整型 （偏移量+4）
        /// </summary>
        /// <param name="sourceBuffer"></param>
        /// <param name="startOffset"></param>
        /// <param name="nextStartOffset"></param>
        /// <returns></returns>
        public static int DecodeInt(byte[] sourceBuffer, int startOffset, out int nextStartOffset)
        {
            //var inBytes = new byte[4];
            //Buffer.BlockCopy(sourceBuffer, startOffset, inBytes, 0, 4);
            nextStartOffset =startOffset+ 4;

            return BitConverter.ToInt32(sourceBuffer,startOffset);
        }
        /// <summary>
        /// 获取64位整型 （偏移量+8）
        /// </summary>
        /// <param name="sourceBuffer"></param>
        /// <param name="startOffset"></param>
        /// <param name="nextStartOffset"></param>
        /// <returns></returns>
        public static long DecodeLong(byte[] sourceBuffer, int startOffset, out int nextStartOffset)
        {
            nextStartOffset = startOffset + 8;
            return BitConverter.ToInt64(sourceBuffer, startOffset);
        }
        /// <summary>
        /// 获取时间（偏移量+8）
        /// </summary>
        /// <param name="sourceBuffer"></param>
        /// <param name="startOffset"></param>
        /// <param name="nextStartOffset"></param>
        /// <returns></returns>
        public static DateTime DecodeDateTime(byte[] sourceBuffer, int startOffset, out int nextStartOffset)
        {
            nextStartOffset = startOffset + 8;
            return new DateTime(BitConverter.ToInt64(sourceBuffer, startOffset));
        }
        /// <summary>
        /// 加密字符串
        /// </summary>
        /// <param name="sourceBuffer">元数组</param>
        /// <param name="startOffset">偏移起始位置</param>
        /// <param name="nextStartOffset">处理结束后的偏移位置</param>
        /// <returns>数据域</returns>
        private static byte[] DecodeBytes(byte[] sourceBuffer, int startOffset, out int nextStartOffset)
        {
            var lenghtBytes = new byte[4];
            Buffer.BlockCopy(sourceBuffer, startOffset, lenghtBytes, 0, 4);
            startOffset += 4;

            var length = BitConverter.ToInt32(lenghtBytes, 0);
            var dataBytes = new byte[length];
            Buffer.BlockCopy(sourceBuffer, startOffset, dataBytes, 0, length);
            startOffset += length;

            nextStartOffset = startOffset;
            return dataBytes;
        }
        /// <summary>
        /// 合并byte数组
        /// </summary>
        /// <param name="arrays"></param>
        /// <returns></returns>
        public static byte[] Combine(params byte[][] arrays)
        {
            byte[] destination = new byte[arrays.Sum(p => p.Length)];
            int offset = 0;
            foreach(byte[] data in arrays)
            {
                Buffer.BlockCopy(data, 0, destination, offset, data.Length);
                offset += data.Length;
            }
            return destination;
        }
    }
}
