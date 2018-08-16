using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace FWService
{
    public partial class FWService : ServiceBase
    {
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
    }
}
