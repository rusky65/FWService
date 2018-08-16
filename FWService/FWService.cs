﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FWService
{
    public partial class FWService : ServiceBase
    {
        const int SERVICE_ACCEPT_PRESHUTDOWN = 0x100;
        const int SERVICE_CONTROL_PRESHUTDOWN = 0xf;

        List<Watcher> watchersList = new List<Watcher>();

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

        /// <summary>
        /// It's a method to help for debugging
        /// </summary>
        /// <param name="args"></param>
        public void RunAsConsole(string[] args)
        {

            OnStart(args);

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();

            OnStop();
        }

        private void ReadWatcherConfig()
        {
            string prgPath = AppDomain.CurrentDomain.BaseDirectory;
            XmlDocument xd = new XmlDocument();

            try
            {
                xd.Load(prgPath + "config_FWService.xml");

                XmlNodeList watcherNodesList = xd.SelectSingleNode("watchers").SelectNodes("watcher");

                foreach (XmlNode watcherItem in watcherNodesList)
                {
                    //Watcher details from xml
                    string wName = watcherItem.Attributes.GetNamedItem("name").Value;
                    string wFilter = watcherItem.Attributes.GetNamedItem("filter").Value;
                    string wPath = watcherItem.Attributes.GetNamedItem("path").Value;
                    bool watchDir = watcherItem.Attributes.GetNamedItem("watch_type").Value.Equals("directory");
                    bool watchSubDir = watcherItem.Attributes.GetNamedItem("subdir").Value.Equals("true");
                    string wMessageTitle = watcherItem.Attributes.GetNamedItem("messageTitle").Value;

                    //Create a watcher
                    Watcher fsWatcher = new Watcher(wName, wPath, wFilter, watchDir, watchSubDir, wMessageTitle);
                    try
                    {
                        watchersList.Add(fsWatcher);
                    }
                    catch (Exception ex)
                    {
                        System.Console.Write(ex.ToString());
                    }

                    //Check the file last modify date, if it is a file watcher
                    if (watcherItem.HasChildNodes)
                    {
                        DateTime storedFileLastModify_dt = Convert.ToDateTime(watcherItem.FirstChild.Attributes.GetNamedItem("modify_dt").Value);
                        if (System.IO.File.Exists(wPath + wFilter))
                        {
                            FileInfo file = new FileInfo(wPath + wFilter);
                            DateTime fileLastModify_dt = Convert.ToDateTime(file.LastWriteTime.ToString("yyyy.MM.dd HH:mm"));
                            if (DateTime.Compare(storedFileLastModify_dt, fileLastModify_dt) < 0)
                            {
                                fsWatcher.WatcherEventHandler(fsWatcher.Name, $"\r\n ------------------\r\nFile / Directory was changed.\r\n{storedFileLastModify_dt} since {wMessageTitle} !\r\nChanging time: {fileLastModify_dt}", wMessageTitle);
                            }
                        }
                    }
                }
            }
            catch (XmlException xmlEx)
            {
                eventLogFwService.WriteEntry(xmlEx.ToString(), EventLogEntryType.Error, eventId++);
            }
            catch (DirectoryNotFoundException dirEx)
            {
                eventLogFwService.WriteEntry(dirEx.ToString(), EventLogEntryType.Error, eventId++);
            }
            catch (FileNotFoundException fileEx)
            {
                eventLogFwService.WriteEntry(fileEx.ToString(), EventLogEntryType.Error, eventId++);
            }
            catch (IOException ioEx)
            {
                eventLogFwService.WriteEntry(ioEx.ToString(), EventLogEntryType.Error, eventId++);
            }
            catch (Exception ex)
            {
                eventLogFwService.WriteEntry(ex.ToString(), EventLogEntryType.Error, eventId++);
            }

        }
    }
}
