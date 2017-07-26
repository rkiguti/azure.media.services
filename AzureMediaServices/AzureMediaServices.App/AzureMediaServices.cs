using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.WindowsAzure.MediaServices.Client;
using UploadProgressChangedEventArgs = Microsoft.WindowsAzure.MediaServices.Client.UploadProgressChangedEventArgs;

namespace AzureMediaServices.App
{
	public class AzureMediaServices
	{
		#region Campos

		private readonly Lazy<CloudMediaContext> _cloudMediaContextLazy;

		#endregion

		#region Constantes
		
		private const string MEDIA_PROCESSOR_NAME = "Media Encoder Standard";
		private const string VIDEO_TASK_CONFIGURATION = "Adaptive Streaming";
		private const string AUDIO_TASK_CONFIGURATION = "AAC Stereo for Streaming"; 

		#endregion

		#region Construtores

		public AzureMediaServices(string aadTenantDomain, string restApiUrl, string clientId, string clientSecret)
		{
			_cloudMediaContextLazy = new Lazy<CloudMediaContext>(() =>
			{
				var tokenCredentials = new AzureAdTokenCredentials(aadTenantDomain, new AzureAdClientSymmetricKey(clientId, clientSecret),
					AzureEnvironments.AzureCloudEnvironment);

				var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

				return new CloudMediaContext(new Uri(restApiUrl), tokenProvider);
			});
		}

		#endregion

		#region Métodos

		public IAsset UploadFile(string fileName, AssetCreationOptions options,
			Action<IAssetFile, UploadProgressChangedEventArgs> uploadProgressChangedCallback = null)
		{
			var inputAsset = _cloudMediaContextLazy.Value.Assets.CreateFromFile(fileName, options, uploadProgressChangedCallback);

			return inputAsset;
		}

		public IAsset EncodeVideoToAdaptiveBitrate(IAsset asset, AssetCreationOptions options, string outputAssetName, out IJob job,
			Action<IJob> executionProgressChangedCallback = null)
		{
			job = _cloudMediaContextLazy.Value.Jobs.CreateWithSingleTask(MEDIA_PROCESSOR_NAME, VIDEO_TASK_CONFIGURATION, asset,
				outputAssetName, options);

			job.Submit();

			job = job.StartExecutionProgressTask(executionProgressChangedCallback, CancellationToken.None).Result;

			var outputAsset = job.OutputMediaAssets[0];

			return outputAsset;
		}

		public IAsset EncodeVideoToAdaptiveBitrate(IAsset asset, AssetCreationOptions options, string outputAssetName,
			Action<IJob> executionProgressChangedCallback = null)
		{
			return EncodeVideoToAdaptiveBitrate(asset, options, outputAssetName, out var _, executionProgressChangedCallback);
		}

		public IAsset EncodeAudioToAacStereo(IAsset asset, AssetCreationOptions options, string outputAssetName, out IJob job,
			Action<IJob> executionProgressChangedCallback = null)
		{
			job = _cloudMediaContextLazy.Value.Jobs.CreateWithSingleTask(MEDIA_PROCESSOR_NAME, AUDIO_TASK_CONFIGURATION, asset,
				outputAssetName, options);

			job.Submit();

			job = job.StartExecutionProgressTask(executionProgressChangedCallback, CancellationToken.None).Result;

			var outputAsset = job.OutputMediaAssets[0];

			return outputAsset;
		}

		public IAsset EncodeAudioToAacStereo(IAsset asset, AssetCreationOptions options, string outputAssetName,
			Action<IJob> executionProgressChangedCallback = null)
		{
			return EncodeAudioToAacStereo(asset, options, outputAssetName, out var _, executionProgressChangedCallback);
		}

		private void CreateOnDemandLocator(IAsset asset)
		{
			_cloudMediaContextLazy.Value.Locators.Create(
				LocatorType.OnDemandOrigin,
				asset,
				AccessPermissions.Read,
				TimeSpan.FromDays(30));
		}

		public void PublishAssetForStreaming(IAsset asset, out Uri smoothStreamingUri, out Uri hlsUri, out Uri mpegDashUri)
		{
			CreateOnDemandLocator(asset);

			smoothStreamingUri = asset.GetSmoothStreamingUri();
			hlsUri = asset.GetHlsUri();
			mpegDashUri = asset.GetMpegDashUri();
		}

		public IAsset GetAsset(string assetId)
		{
			var assetInstance =
				from a in _cloudMediaContextLazy.Value.Assets
				where a.Id == assetId
				select a;

			var asset = assetInstance.FirstOrDefault();

			return asset;
		}

		public void DeleteAsset(IAsset asset)
		{
			asset.Delete();
		}

		public IJob GetJob(string jobId)
		{
			var jobInstance =
				from j in _cloudMediaContextLazy.Value.Jobs
				where j.Id == jobId
				select j;
			
			var job = jobInstance.FirstOrDefault();

			return job;
		}

		public void DeleteJob(string jobId)
		{
			var jobDeleted = false;

			while (!jobDeleted)
			{
				var job = GetJob(jobId);

				switch (job.State)
				{
					case JobState.Finished:
					case JobState.Canceled:
					case JobState.Error:
						job.Delete();
						jobDeleted = true;
						break;

					case JobState.Canceling:
						Thread.Sleep(5000);
						break;

					case JobState.Queued:
					case JobState.Scheduled:
					case JobState.Processing:
						job.Cancel();
						break;

					default:
						throw new ArgumentException("JobState invalid");
				}

			}
		}

		public IEnumerable<IAsset> ListAssets()
		{
			return _cloudMediaContextLazy.Value.Assets;
		}

		public long TotalFileSize(IAsset asset)
		{
			long totalFileSize = 0;

			foreach (var assetFile in asset.AssetFiles)
			{
				totalFileSize += assetFile.ContentFileSize;
			}

			return totalFileSize;
		}

		public long TotalFileSize()
		{
			long totalFileSize = 0;

			foreach (var asset in ListAssets())
			{
				totalFileSize += TotalFileSize(asset);
			}

			return totalFileSize;
		}

		public void DownloadAsset(IAsset asset, string outputDirectory)
		{
			foreach (var file in asset.AssetFiles)
			{
				file.Download(Path.Combine(outputDirectory, file.Name));
			}
		}

		#endregion
	}
}