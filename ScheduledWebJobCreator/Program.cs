using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;
using Microsoft.WindowsAzure.Management.Scheduler.Models;
using Microsoft.WindowsAzure.Management.WebSites;
using Microsoft.WindowsAzure.Management.WebSites.Models;
using Microsoft.WindowsAzure.Scheduler;
using Microsoft.WindowsAzure.Scheduler.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ScheduledWebJobCreator
{
    class Program
    {
        private CloudServiceManagementClient _cloudServiceManagementClient;
        private WebSiteManagementClient _webSiteMgmtClient;
        private SchedulerManagementClient _schedulerMgmtClient;
        private TokenCloudCredentials _credential;
        private SchedulerClient _schedulerClient;

        public Program(ScheduledWebJobCreatorParameters parms)
        {
            // auth
            _credential = TokenCredentialHelper<MyPersonalConfiguration>.GetCredentials(
                new MyPersonalConfiguration(parms),
                parms.subscriptionId);

            // create the clients needed
            _cloudServiceManagementClient = CloudContext.Clients.CreateCloudServiceManagementClient(_credential);
            _webSiteMgmtClient = CloudContext.Clients.CreateWebSiteManagementClient(_credential);
            _schedulerMgmtClient = CloudContext.Clients.CreateSchedulerManagementClient(_credential);
        }

        static void Main(string[] args)
        {
            if (!File.Exists("webJob.json"))
            {
                Console.WriteLine("Please create a JSON file named webJob.json containing the properties needed for execution");
            }
            else
            {
                using (FileStream file = File.OpenRead("webJob.json"))
                {
                    var json = new StreamReader(file).ReadToEnd();
                    var parms = JsonConvert.DeserializeObject<ScheduledWebJobCreatorParameters>(json);

                    Program p = new Program(parms);
                    parms.webJobs.ForEach(w => p.CreateWebJob(w));
                }
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }

        void CreateWebJob(WebJobParameter parameters)
        {
            Console.WriteLine("Getting the web space list");

            var webSpaceListResult = _webSiteMgmtClient.WebSpaces.List();

            var webSpace = webSpaceListResult.WebSpaces.First(x => x.GeoRegion == parameters.regionName);

            // create the web site
            Console.WriteLine("Checking for existing site");

            try
            {
                // in a try/catch because the 404 will yield an error if the site isn't found
                _webSiteMgmtClient.WebSites.Get(webSpace.Name, parameters.webSiteName, new WebSiteGetParameters());
            }
            catch
            {
                // if it wasn't found, we need it, so let's create it
                Console.WriteLine("Creating the web site");

                _webSiteMgmtClient.WebSites.Create(webSpace.Name, new WebSiteCreateParameters
                    {
                        Name = parameters.webSiteName,
                        WebSpaceName = webSpace.Name
                    });
            }

            // get the web site
            Console.WriteLine("Getting the web site's username & password");

            var webSiteGetResult = _webSiteMgmtClient.WebSites.Get(webSpace.Name,
                parameters.webSiteName,
                new WebSiteGetParameters
                {
                    PropertiesToInclude = new string[] { "PublishingUsername", "PublishingPassword" }
                });

            var username = webSiteGetResult.WebSite.SiteProperties.Properties["PublishingUsername"];
            var password = webSiteGetResult.WebSite.SiteProperties.Properties["PublishingPassword"];

            // create the webjob
            var kuduClient = CloudContext.Clients.CreateWebSiteExtensionsClient(new BasicAuthenticationCloudCredentials
            {
                Password = password,
                Username = username,
            }, parameters.webSiteName);

            using (FileStream stream = File.Open(parameters.filePath, FileMode.Open))
            {
                Console.WriteLine("Creating the WebJob and uploading the Console App");
                var webJobCreateResult = kuduClient.WebJobs.UploadTriggered(parameters.webJobName, stream);
            }

            // build the cloud/job service names based on the web site name
            var cloudServiceName = string.Format("{0}SchedulerJobsCloudService", parameters.webSiteName);
            var jobCollectionName = string.Format("{0}SchedulerJobsCollection", parameters.webSiteName);

            // create the cloud service for the scheduler jobs
            if (!_cloudServiceManagementClient.CloudServices.List().CloudServices.Any(x => x.Name == cloudServiceName))
            {
                Console.WriteLine("Creating the scheduler cloud service");

                var cloudServiceCreateResult = _cloudServiceManagementClient.CloudServices.Create(cloudServiceName,
                    new CloudServiceCreateParameters
                    {
                        GeoRegion = parameters.regionName,
                        Label = cloudServiceName,
                        Description = string.Format("Scheduled Jobs for WebJob {0}", parameters.webJobName)
                    });
            }

            Console.WriteLine("Creating the scheduler job collection");

            // create the scheduler job collection
            try
            {
                _schedulerMgmtClient.JobCollections.Create(cloudServiceName, jobCollectionName,
                    new JobCollectionCreateParameters
                    {
                        Label = jobCollectionName,
                        IntrinsicSettings = new JobCollectionIntrinsicSettings
                        {
                            Plan = JobCollectionPlan.Free,
                            Quota = new JobCollectionQuota
                            {
                                MaxJobCount = 5,
                                MaxJobOccurrence = 1,
                                MaxRecurrence = new JobCollectionMaxRecurrence
                                {
                                    Frequency = JobCollectionRecurrenceFrequency.Hour,
                                    Interval = 1
                                }
                            }
                        }
                    });
            }
            catch
            {
                // job collection was already there
            }

            // get the webjob's details
            var webJob = kuduClient.WebJobs.GetTriggered(parameters.webJobName);

            // create the scheduler job
            Console.WriteLine("Creating the scheduler job");

            _schedulerClient = new SchedulerClient(_credential, cloudServiceName, jobCollectionName);

            var request =
                new JobHttpRequest
                {
                    // build the Uri for the REST call to run a webjob
                    Uri = new Uri(string.Format("{0}/run", webJob.WebJob.Url.AbsoluteUri)),
                    Method = "POST"
                };

            // build the base64 string to use for authorizing
            var rawAuthorizationString = string.Format("{0}:{1}", username, password);
            var encryptedAuthorizationString =
                Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(rawAuthorizationString));

            // add the headers to the request
            request.Headers.Add("authorization", string.Format("Basic {0}", encryptedAuthorizationString));
            request.Headers.Add("content-Type", "text/plain");

            // create the job
            _schedulerClient.Jobs.Create(new JobCreateParameters
            {
                Action = new JobAction
                {
                    Type = JobActionType.Https,
                    Request = request
                },
                Recurrence = new JobRecurrence
                {
                    Frequency = parameters.jobRecurrenceFrequency,
                    Interval = parameters.interval,
                    EndTime = parameters.endTime
                },
                StartTime = parameters.startTime
            });

            Console.WriteLine("Job Created");
        }
    }
}
