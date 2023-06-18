﻿using Solocast.Core.Interfaces;
using Solocast.Services.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;

namespace Solocast.Services
{
    public class FileDownloadService : IFileDownloadService
    {
        private Dictionary<string, DownloadOperation> downloads;
        private Dictionary<string, CancellationTokenSource> cancellationTokenSources;

        public FileDownloadService()
        {
            downloads = new Dictionary<string, DownloadOperation>();
            cancellationTokenSources = new Dictionary<string, CancellationTokenSource>();
        }

        public int NumberOfDownlaods
        {
            get
            {
                return downloads.Count;
            }
        }

        public void CancelAllDownloads()
        {
            foreach (var cancellationTokenSource in cancellationTokenSources.ToList())
            {
                CancelDownload(cancellationTokenSource.Key);
            }
        }

        public void CancelDownload(string fileUrl)
        {
            var cancellationTokenSource = cancellationTokenSources[fileUrl];
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSources.Remove(fileUrl);
            downloads.Remove(fileUrl);
        }

        public async Task<StorageFile> DownloadFileAsync(
            string appFolderName,
            string folderName,
            string fileName,
            string fileUrl,
            Action<DownloadOperation> callback,
            Action<Exception> errorCallback = null)
        {
            var appFolder = await KnownFolders.MusicLibrary.CreateFolderAsync(appFolderName, CreationCollisionOption.OpenIfExists);
            var podcastFolder = await appFolder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
            var uri = new Uri(fileUrl);
            var extension = uri.AbsolutePath.GetExtension();
            var filename = (fileName + extension).RemoveIllegalPathChars();

            try
            {
                var file = await podcastFolder.CreateFileAsync(filename, CreationCollisionOption.FailIfExists);
                var backgroundDownloader = new BackgroundDownloader();
                var downloadOperation = backgroundDownloader.CreateDownload(uri, file);

                var progress = new Progress<DownloadOperation>(callback);
                downloads.Add(fileUrl, downloadOperation);

                var cts = new CancellationTokenSource();
                cancellationTokenSources.Add(fileUrl, cts);

                await downloadOperation.StartAsync().AsTask(cts.Token, progress);

                downloads.Remove(fileUrl);
                cancellationTokenSources.Remove(fileUrl);
                return file;
            }
            catch (NullReferenceException ex)
            {
                if (errorCallback != null)
                    errorCallback(ex);
                return null;
            }
            catch (Exception)
            {
                try
                {
                    var file = await podcastFolder.GetFileAsync(filename);
                    return file;
                }
                catch (Exception ex)
                {
                    if (errorCallback != null)
                        errorCallback(ex);
                    return null;
                }
            }

        }

        public void PauseDownload(string fileUrl)
        {
            var downloadOperation = downloads[fileUrl];
            if (downloadOperation.Progress.Status == BackgroundTransferStatus.Running)
            {
                downloadOperation.Pause();
            }
        }

        public void ResumeDownload(string fielUrl)
        {
            var downloadOperation = downloads[fielUrl];
            if (downloadOperation.Progress.Status == BackgroundTransferStatus.PausedByApplication)
            {
                downloadOperation.Resume();
            }
        }
    }
}
