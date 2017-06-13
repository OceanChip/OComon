using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class EndPointExtensions
    {
        public static string ToAddress(this EndPoint endPoint)
        {
            Check.NotNull(endPoint, nameof(endPoint));
            return ((IPEndPoint)endPoint).ToAddress();
        }
        public static string ToAddress(this IPEndPoint endPoint)
        {
            Check.NotNull(endPoint, nameof(endPoint));
            return $"{endPoint.Address}:{endPoint.Port}";
        }
        /// <summary>
        ///  将ip字符串转换为IPEndPointl类型，每组ip之间用‘,’隔开
        /// 例如：192.168.5.6:5000
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static IPEndPoint ToEndPoint(this string address)
        {
            Check.NotNull(address, nameof(address));
            var array = address.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length != 2)
                throw new Exception("无效地址:" + address);

            var ip = IPAddress.Parse(array[0]);
            var port = int.Parse(array[1]);
            return new IPEndPoint(ip, port);
        }
        /// <summary>
        /// 将ip字符串转换为IPEndPointl类型，每组ip之间用‘,’隔开
        /// 例如：192.168.5.6:5000,129.234.12.15:3000
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static IEnumerable<IPEndPoint> ToEndPoints(this string address)
        {
            Check.NotNull(address, nameof(address));
            var array = address.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<IPEndPoint>();
            foreach(var item in array)
            {
                list.Add(item.ToEndPoint());
            }
            return list;
        }
    }
}
