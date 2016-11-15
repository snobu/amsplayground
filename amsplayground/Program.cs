using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace amsplayground
{
    class Program
    {
        // Context
        private static string accKey = ConfigurationManager.AppSettings["accKey"];
        private static string accName = ConfigurationManager.AppSettings["accName"];
        private static CloudMediaContext mediaContext;

        // Input files
        private static string _inputfile1080p = Path.GetFullPath(@"d:\_monday_ams\Bitten By The Frost 1080p.mp4");

        static void Main(string[] args)
        {
            // DEBUG
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DEBUG] Media Services account name from App Settings: {accName}");
            Console.ResetColor();

            // Context
            mediaContext = new CloudMediaContext(accName, accKey);

            // Get reserved units
            // https://azure.microsoft.com/en-us/documentation/articles/media-services-scale-media-processing-overview/
            string unit_type = "Basic";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Reserved Encoding Units: {GetReservedUnits(unit_type)}, Type: {unit_type}");
            Console.ResetColor();

            // Set reserved units
            //     Basic = S1
            //     Standard = S2
            //     Premium = S3
            SetReservedUnits(unit_type, 1);

            // Get/Set streaming units
            List<IStreamingEndpoint> endpoints = mediaContext.StreamingEndpoints.ToList();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Streaming Endpoints: ");
            foreach (IStreamingEndpoint endpoint in endpoints)
            {
                Console.WriteLine($"    ((-)) {endpoint.Name}, Units: {endpoint.ScaleUnits} " +
                    $"State: {endpoint.State}");
            }
            Console.ResetColor();

            // Create asset and upload
            IAsset asset = CreateAssetAndUpload(_inputfile1080p);
            Console.WriteLine("Asset created and file uploaded.");

            // Generate streaming locator
            Console.WriteLine("((-)) Generating locator URL...");
            GetLocator(mediaContext.Assets.First());

            Console.WriteLine("All Done. Hit any key to exit.");
            Console.ReadKey();
        }

        static public IAsset CreateAssetAndUpload(string inputfile)
        {
            #region
            // List MediaProcessors
            /*
            var processor = mediaContext.MediaProcessors.ToList();
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("Available Media Processors:\n");
            foreach (var item in processor)
            {
                Console.WriteLine($"{item.Description}, Version {item.Version}");
            }
            Console.WriteLine("-------------------------------------------");
            */
            #endregion

            // Create asset
            Console.WriteLine("Creating asset...");
            var assetName = "Bitten by the Frost";
            var asset = mediaContext.Assets.Create(assetName, AssetCreationOptions.None); // Set encryption at rest here

            // Create file object associated with asset
            var fileName = Path.GetFileName(inputfile);
            var assetFile = asset.AssetFiles.Create(fileName);
            Console.WriteLine($"Created assetFile {assetFile.Name}");

            // Create access policy and SAS locator (needed for upload)
            var policy = mediaContext.AccessPolicies.Create("Write", TimeSpan.FromHours(1), AccessPermissions.Write);
            var locator = mediaContext.Locators.CreateSasLocator(asset, policy);

            // Upload to asset file object (you upload to assetFile not to asset)
            Console.WriteLine($"Uploading {assetFile.Name} [{assetFile.MimeType}]");

            // Use the UploadAsync method to ensure that the calls are not blocking
            // and the files are uploaded in parallel.
            // https://azure.microsoft.com/en-gb/documentation/articles/media-services-dotnet-upload-files/
            var blobTransferClient = new BlobTransferClient();
            blobTransferClient.NumberOfConcurrentTransfers = 5;
            blobTransferClient.ParallelTransferThreadCount = 5;
            blobTransferClient.TransferProgressChanged += UploadProgress;
            assetFile.UploadAsync(inputfile, blobTransferClient, locator, new CancellationToken()).Wait();
            Console.WriteLine($"\nUpload finished: {assetFile.Name}");

            // Select default (primary) file within asset
            assetFile.IsPrimary = true;

            // Cleanup
            locator.Delete();

            // Kick off encoding job
            Console.WriteLine("Adding encoding job..");
            IJob job = CreateEncodingJob(asset);

            // Show task (encode) progress
            GetTaskProgress(job).Wait();

            return asset;
        }

        static private IJob CreateEncodingJob(IAsset asset)
        {
            // Declare a new job
            IJob job = mediaContext.Jobs.Create("Encode to SD MP4");

            // Encoding profile
            string profile = "H264 Multiple Bitrate 16x9 SD";
            Console.WriteLine($"Encoding Profile: {profile}");

            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = mediaContext.MediaProcessors.
                Where(p => p.Name == "Media Encoder Standard").ToList().
                 OrderBy(p => new Version(p.Version)).LastOrDefault();

            // Add task to job
            ITask task = job.Tasks.AddNew("Encode to SD MP4 task",
                                          processor,
                                          profile,
                                          TaskOptions.None);

            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew("Bitten by the Frost SD", AssetCreationOptions.None);

            // Hook an event handler for job state changes
            // job.StateChanged += new EventHandler<JobStateChangedEventArgs>(JobStateChangedHandler);
            job.Submit();
            //job.GetExecutionProgressTask(new CancellationToken()).Wait();
            Console.WriteLine("Encoding job submitted.");

            return job;
        }

        static private void GetLocator(IAsset asset)
        {
            var manifest = from f in asset.AssetFiles
                           where f.Name.EndsWith(".ism")
                           select f;

            IAssetFile manifestFile = manifest.First();

            IAccessPolicy policy = mediaContext.AccessPolicies.Create("Streaming policy",
                TimeSpan.FromDays(90),
                AccessPermissions.Read);

            ILocator locator = mediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin,
                asset,
                policy,
                DateTime.UtcNow.AddMinutes(-5)); // Compensate for clock drift

            Console.WriteLine($"Base origin: {locator.Path}");

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            string streamurl = locator.Path + Uri.EscapeUriString(manifestFile.Name) + "/manifest";

            // HLS
            string hsl = String.Format("{0}{1}", streamurl, "(format=m3u8-aapl)");

            Console.WriteLine("HLS manifest for streaming: ");
            Console.WriteLine(hsl);
        }

        static private int GetReservedUnits(string unit_type)
        {
            int units = -1; // You get -1 if we're unsuccessful in calling the API
            IEncodingReservedUnit encodingReservedUnits = mediaContext.EncodingReservedUnits.FirstOrDefault();
            switch (unit_type)
            {
                case "Basic":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Basic;
                    break;
                case "Standard":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Standard;
                    break;
                case "Premium":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Standard;
                    break;
            }
            if (encodingReservedUnits.CurrentReservedUnits >= 0)
            {
                units = encodingReservedUnits.CurrentReservedUnits;
            }

            return units;
        }

        static private void SetReservedUnits(string unit_type, int desired_units)
        {
            IEncodingReservedUnit encodingReservedUnits = mediaContext.EncodingReservedUnits.FirstOrDefault();
            switch (unit_type)
            {
                case "Basic":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Basic;
                    break;
                case "Standard":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Standard;
                    break;
                case "Premium":
                    encodingReservedUnits.ReservedUnitType = ReservedUnitType.Premium;
                    break;
            }
            encodingReservedUnits.CurrentReservedUnits = desired_units;
            encodingReservedUnits.Update();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Reserved Units Set to {encodingReservedUnits.CurrentReservedUnits}, " +
                              $"Type: {encodingReservedUnits.ReservedUnitType}");
            Console.ResetColor();
        }

        static void UploadProgress(object sender, BlobTransferProgressChangedEventArgs e)
        {
            for (int i = 0; i < 70; i++)
            {
                Console.Write("\b");
            }
            Console.Write($"Progress {e.ProgressPercentage.ToString()} %");
        }

        private async static Task<IJob> GetTaskProgress(IJob job)
        {
            // Start a task to monitor the job progress by invoking a callback
            // when its state or overall progress change in a single extension method.
            Console.ForegroundColor = ConsoleColor.Yellow;
            job = await job.StartExecutionProgressTask(
                j =>
                {
                    Console.WriteLine($"Current job state: {j.State}");
                    Console.WriteLine($"Current job progress: {Math.Round(j.GetOverallProgress(), 2)}% done");
                },
                CancellationToken.None);
            Console.ResetColor();

            return job;
        }
    }
}