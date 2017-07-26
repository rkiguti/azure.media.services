using System;
using System.Configuration;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace AzureMediaServices.App
{
	class Program
	{
		private static readonly string _aadTenantDomain = ConfigurationManager.AppSettings["AADTenantDomain"];
		private static readonly string _restApiUrl = ConfigurationManager.AppSettings["RESTApiUrl"];
		private static readonly string _clientId = ConfigurationManager.AppSettings["ClientId"];
		private static readonly string _clientSecret = ConfigurationManager.AppSettings["ClientSecret"];

		// Field for service context.
		private static AzureMediaServices azureMediaServices = null;

		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine($"Start: {DateTime.Now}");
				Console.WriteLine();

				azureMediaServices = new AzureMediaServices(_aadTenantDomain, _restApiUrl, _clientId, _clientSecret);

				var videoAsset = UploadFile(@"D:\DRIVE_C\Downloads\06. Demo - Basics.mp4");
				var audioAsset = UploadFile(@"D:\Guide 6 Understand.mp3");

				var videoEncodedAsset = EncodeVideo(videoAsset, "videoAsset", out var videoJob);
				var audioEncodedAsset = EncodeAudio(audioAsset, "audioAsset", out var audioJob);

				PublishAssetGetURLs(videoEncodedAsset);
				PublishAssetGetURLs(audioEncodedAsset);

				azureMediaServices.DeleteJob(videoJob.Id);
				azureMediaServices.DeleteJob(audioJob.Id);

				azureMediaServices.DeleteAsset(videoAsset);
				azureMediaServices.DeleteAsset(audioAsset);

				PrintTotalFileSize();

				Console.WriteLine($"Finish: {DateTime.Now}");
			}
			catch (Exception exception)
			{
				// Parse the XML error message in the Media Services response and create a new
				// exception with its content.
				exception = MediaServicesExceptionParser.Parse(exception);

				Console.Error.WriteLine(exception.Message);
			}
			finally
			{
				Console.ReadLine();
			}
		}

		static public IAsset UploadFile(string fileName)
		{
			IAsset inputAsset = azureMediaServices.UploadFile(fileName, AssetCreationOptions.None, 
				(af, p) =>
				{
					Console.WriteLine("Uploading '{0}' - Progress: {1:0.##}%", af.Name, p.Progress);
				});

			Console.WriteLine("Asset {0} created.", inputAsset.Id);

			return inputAsset;
		}

		static public IAsset EncodeVideo(IAsset asset, string outputName, out IJob job)
		{
			Console.WriteLine("Submitting video transcoding job...");

			var outputAsset = azureMediaServices.EncodeVideoToAdaptiveBitrate(asset, AssetCreationOptions.None, outputName, out job, 
				j =>
				{
					Console.WriteLine("Job state: {0}", j.State);
					Console.WriteLine("Job progress: {0:0.##}%", j.GetOverallProgress());
				});

			Console.WriteLine("Video transcoding job finished.");

			return outputAsset;
		}

		static public IAsset EncodeAudio(IAsset asset, string outputName, out IJob job)
		{
			Console.WriteLine("Submitting audio transcoding job...");

			var outputAsset = azureMediaServices.EncodeAudioToAacStereo(asset, AssetCreationOptions.None, outputName, out job,
				j =>
				{
					Console.WriteLine("Job state: {0}", j.State);
					Console.WriteLine("Job progress: {0:0.##}%", j.GetOverallProgress());
				});

			Console.WriteLine("Audio transcoding job finished.");

			return outputAsset;
		}

		static public void PublishAssetGetURLs(IAsset asset)
		{
			azureMediaServices.PublishAssetForStreaming(asset, out var smoothStreamingUri, out var hlsUri, out var mpegDashUri);

			// Display  the streaming URLs.
			Console.WriteLine("Use the following URLs for adaptive streaming: ");
			Console.WriteLine(smoothStreamingUri);
			Console.WriteLine(hlsUri);
			Console.WriteLine(mpegDashUri);
			Console.WriteLine();
		}

		static public void PrintTotalFileSize()
		{
			Console.WriteLine($"Total file size: {azureMediaServices.TotalFileSize() / 1000.0 / 1000.0:0.00} MB");
			Console.WriteLine();
		}
	}
}
