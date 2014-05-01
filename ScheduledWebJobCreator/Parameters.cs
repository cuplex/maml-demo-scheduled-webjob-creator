using Microsoft.WindowsAzure.Scheduler.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace ScheduledWebJobCreator
{
    public class ScheduledWebJobCreatorParameters
    {
        public ScheduledWebJobCreatorParameters()
        {
            this.webJobs = new List<WebJobParameter>();
        }

        public List<WebJobParameter> webJobs { get; set; }
        public string subscriptionId { get; set; }
        public string activeDirectoryTenantId { get; set; }
        public string activeDirectoryClientId { get; set; }
        public string activeDirectoryRedirectUrl { get; set; }
    }

    public class WebJobParameter
    {
        public string regionName { get; set; }
        public string webSiteName { get; set; }
        public string filePath { get; set; }
        public string webJobName { get; set; }
        public DateTime startTime { get; set; }
        public DateTime? endTime { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public JobRecurrenceFrequency jobRecurrenceFrequency { get; set; }

        public int interval { get; set; }
    }
}
