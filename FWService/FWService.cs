using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace FWService
{
    public partial class FWService : ServiceBase
    {
        const int SERVICE_ACCEPT_PRESHUTDOWN = 0x100;
        const int SERVICE_CONTROL_PRESHUTDOWN = 0xf;

        public FWService()
        {
            InitializeComponent();

            #region Setting the event log for this service
            eventLogFwService = new EventLog();
            if (!EventLog.SourceExists("FWServiceSource"))
            {
                EventLog.CreateEventSource("FWServiceSource", "FWServiceLog");
            }

            eventLogFwService.Source = "FWServiceSource";
            eventLogFwService.Log = "FWServiceLog";
            #endregion Setting the event log for this service

            FieldInfo acceptedCommandsFieldInfo =
                typeof(ServiceBase).GetField("acceptedCommands", BindingFlags.Instance | BindingFlags.NonPublic);

            if (acceptedCommandsFieldInfo == null)
                throw new ApplicationException("acceptedCommands field not found");

            int value = (int)acceptedCommandsFieldInfo.GetValue(this);
            acceptedCommandsFieldInfo.SetValue(this, value | SERVICE_ACCEPT_PRESHUTDOWN);

        }

        protected override void OnCustomCommand(int command)
        {
            if (command == SERVICE_CONTROL_PRESHUTDOWN)
            {
                Stop();
            }
            else
            {
                base.OnCustomCommand(command);
            }
        }

        protected override void OnStart(string[] args)
        {
            //Writing log for this event
            eventLogFwService.WriteEntry("FWService service is started", EventLogEntryType.Information, eventId++);
        }

        protected override void OnStop()
        {
            //Writing log for this event
            eventLogFwService.WriteEntry("FWService service is stopped", EventLogEntryType.Information, eventId++);
        }

        protected override void OnShutdown()
        {
            //Writing log for this event
            eventLogFwService.WriteEntry("FWService service stopping is executed by OnShutdown methode.", EventLogEntryType.Information, eventId++);

            Stop();
            base.OnShutdown();
        }
    }
}
