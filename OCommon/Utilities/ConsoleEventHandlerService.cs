using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    /// <summary>
    /// 捕获控制台事件服务(例如：CTRL-C,CTRL-Z)
    /// </summary>
    public class ConsoleEventHandlerService:IDisposable
    {
        public enum ConsoleEvent
        {
            CtrlC=0,     //CTRL+C
            CtrlClose=2,
            CtrlLogoff=5,
            CtrlShutdown=6,
        }
        public delegate void ControlEventHandler(int consoleEvent);
        private readonly ControlEventHandler _eventHandler;
        private ControlEventHandler _closingEventHandler;
        public ConsoleEventHandlerService()
        {
            _eventHandler = new ControlEventHandler(consoleEvent =>
              {
                  if (IsCloseEvent(consoleEvent) && _closingEventHandler != null)
                  {
                      _closingEventHandler(consoleEvent);
                  }
                  SetConsoleCtrlHandler(_eventHandler, true);
              });
        }

        private static bool IsCloseEvent(int consoleEvent)
        {
            if((consoleEvent==(int)ConsoleEvent.CtrlC && !Console.TreatControlCAsInput)
                || consoleEvent ==(int)ConsoleEvent.CtrlClose
                || consoleEvent==(int)ConsoleEvent.CtrlLogoff
                || consoleEvent == (int)ConsoleEvent.CtrlShutdown)
            {
                return true;
            }
            return false;

        }

        ~ConsoleEventHandlerService()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public void Dispose(bool disposing)
        {
            SetConsoleCtrlHandler(_eventHandler, false);
        }

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ControlEventHandler e, bool add);
    }
}
