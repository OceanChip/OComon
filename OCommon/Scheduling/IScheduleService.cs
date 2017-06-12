using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Scheduling
{
    public interface IScheduleService
    {
        void StartTask(string name, Action action, int dueTime, int period);
        void StopTask(string name);
    }
}
