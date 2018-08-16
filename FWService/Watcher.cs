using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Timers;
using System.Xml;

namespace FWService
{
    class Watcher
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Filter { get; set; }
        private readonly string OrigFilter;
        public bool WatchDir { get; set; }
        public bool WatchSubDir { get; set; }
        public string Caption { get; set; }

        private StringBuilder allEventSb = new StringBuilder();
        private StringBuilder m_Sb = new StringBuilder();
        private bool m_bDirty;
        private FileSystemWatcher watcher;
        private Timer timerNotify;
        private DateTime lastRead = DateTime.MaxValue;

        private readonly MemoryCache _memoryCach;
        private readonly CacheItemPolicy _cachItemPolicy;
        private const int CachTimeMilliseconds = 1000;

        public Watcher(string name, string path, string filter, bool watchDir, bool watchSubDir, string caption)
        {
            Name = name;
            Path = path;
            Filter = filter;
            OrigFilter = filter;
            WatchDir = watchDir;
            WatchSubDir = watchSubDir;
            Caption = caption;

            _memoryCach = MemoryCache.Default;
            _cachItemPolicy = new CacheItemPolicy()
            {
                RemovedCallback = OnRemovedFromCache
            };

            // Create a new FileSystemWatcher and set its properties.
            watcher = new FileSystemWatcher();

            //Set parameters for proper working
            m_bDirty = false;

            // Set watcher what to watch is in Path and Filter.
            if (Directory.Exists(Path))
            {
                watcher.Path = Path;
            }
            else
            {
                //todo: write log
                //($"Directory ! NOT exists: {Path} in method 'public Watcher(string name, string path, string filter, bool watchDir, bool watchSubDir, string caption)'", AppDomain.CurrentDomain.BaseDirectory + "\\FWServiceServiceOwnLog.txt");
            }
            watcher.Filter = Filter;

            watcher.IncludeSubdirectories = WatchSubDir;

            /* Watch for changes in LastAccess and LastWrite times, and
              the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            // Add event handlers.
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            //Create a timer
            timerNotify = new Timer();
            timerNotify.Interval = 100;
            timerNotify.Elapsed += new ElapsedEventHandler(TimerNotify_Tick);
            timerNotify.Enabled = true;

            // Begin watching.
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Handle cache item expiring
        /// </summary>
        /// <param name="args"></param>
        private void OnRemovedFromCache(CacheEntryRemovedArguments args)
        {
            if (args.RemovedReason != CacheEntryRemovedReason.Expired)
            {
                return;
            }

            // Now actually handle file event
            var e = (FileSystemEventArgs)args.CacheItem.Value;

            if (e != null)
            {
                DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    lastWriteTime = DateTime.Now;
                    if (!WatchDir)
                    {
                        watcher.Filter = OrigFilter;
                    }
                }
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append("\r\n ------------------\r\n");
                m_Sb.Append("FullPath: " + e.FullPath + "\r\n");
                m_Sb.Append("ChangeType: " + e.ChangeType.ToString() + "\r\n");
                m_Sb.Append("Actual date and time: " + DateTime.Now.ToString() + "\r\n");
                m_Sb.Append("Changing time: " + lastWriteTime.ToString());
                m_bDirty = true;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            _cachItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CachTimeMilliseconds);

            // Only add if it is already not there (swallow others)
            _memoryCach.AddOrGetExisting(e.Name, e, _cachItemPolicy);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!m_bDirty)
            {
                DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                //if (lastWriteTime != lastRead) {
                lastRead = lastWriteTime;
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append("ReNAMING !\r\n");
                m_Sb.Append("OldFullPath: " + e.OldFullPath + "\r\n");
                m_Sb.Append("ChangeType: " + e.ChangeType.ToString() + "\r\n");
                m_Sb.Append("to: " + e.Name + "\r\n");
                m_Sb.Append("Actual date and time: " + DateTime.Now.ToString() + "\r\n");
                if (!WatchDir)
                {
                    watcher.Filter = e.Name;
                    watcher.Path = e.FullPath.Substring(0, e.FullPath.Length - watcher.Filter.Length);
                }
                m_bDirty = true;
            }
        }

        private void TimerNotify_Tick(object sender, EventArgs e)
        {
            if (m_bDirty)
            {
                m_bDirty = false;

                // Specify what is done when a file is changed, created, or deleted.
                WatcherEventHandler(Name, m_Sb.ToString(), Caption);
            }
        }

        public void WatcherEventHandler(string watcherName, string eventDescription, string caption)
        {
            //Put the event description to the Container
            allEventSb.Append(eventDescription + "\r\n---------------------------   * * *   ------------------------------\r\n");

            //todo: switch on if it works properly.
            //Sending mail to specified persons which configured in config file.
            //SendMail(watcherName, caption, eventDescription);
        }

        public static void SendMail(string watcherName, string caption, string eventDescription)
        {
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("192.000.000.00");
            smtp.Credentials = new System.Net.NetworkCredential("test_user", "password_of_test_user", "DOMAIN");

            string prgPath = AppDomain.CurrentDomain.BaseDirectory + "\\";
            XmlDocument xd = new XmlDocument();
            xd.Load(prgPath + "config_FWService.xml");

            XmlNode watcherNode = xd.SelectSingleNode("watchers/watcher[@name = '" + watcherName + "']");

            XmlNodeList emailsList = watcherNode.SelectNodes("emails/mail");
            foreach (XmlNode mailItem in emailsList)
            {

                //Getting the rt requests.
                XmlNodeList requestsList = mailItem.SelectNodes("rtrequests/rt");
                string rtRequests = "";
                foreach (XmlNode requestItem in requestsList)
                {
                    rtRequests += requestItem.Attributes.GetNamedItem("id").Value + ", ";
                }
                rtRequests = rtRequests.Substring(0, rtRequests.Length - 2);

                //Getting the mail addresses.
                XmlNodeList addressList = mailItem.SelectNodes("mailto/to");
                string mailTo = "";
                foreach (XmlNode addressItem in addressList)
                {
                    mailTo += addressItem.Attributes.GetNamedItem("address").Value + ',';
                }
                mailTo = mailTo.Substring(0, mailTo.Length - 1);

                message.To.Clear();
                message.To.Add(mailTo);

                message.Subject = caption;
                message.From = new System.Net.Mail.MailAddress("noreply@test.com", "Name of sender");
                message.Body = "Hi,\r\n\r\n" + caption +
                    $" !\r\nThe time of modify:{eventDescription.Substring(eventDescription.Length - 20)}\r\nInvoked request:\r\n" +
                    rtRequests + "\r\n\r\nBest regards\r\nIT";

                smtp.Send(message);
            }
        }

        public string getLog()
        {
            return allEventSb.ToString();
        }
    }
}