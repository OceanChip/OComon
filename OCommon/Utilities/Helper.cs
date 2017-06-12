using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public static  class Helper
    {
        public static void ExecuteActionWithoutException(this Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {

            }
        }
        public static T ExecuteActionWithoutException<T>(this Func<T> action,T defaultValue = default(T))
        {
            try
            {
               return action();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
    }
}
