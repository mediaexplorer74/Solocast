﻿using Solocast.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Solocast.Core.Contracts;
using Windows.Storage;
using System.Diagnostics;
using Solocast.Core.Exceptions;

namespace Solocast.Services
{
    public class PodcastService : IPodcastService
    {
        private IFeedParaseService feedParser;
        private IPodcastStore<Podcast> storageService;
        private IFileDownloadService fileDownloadManager;

        public PodcastService(IFeedParaseService feedParser, IPodcastStore<Podcast> storageService, IFileDownloadService fileDownloadManager)
        {
            this.feedParser = feedParser;
            this.storageService = storageService;
            this.fileDownloadManager = fileDownloadManager;
        }

        public async Task<Podcast> GetPodcastAsync(string feedUrl)
        {
            try
            {
                var podcast = await feedParser.GetPodcastAsync(feedUrl);
                return podcast;
            }
            catch (Exception ex)
            {
                throw new GetPodcastException(feedUrl, ex);
            }
        }

        public async Task<IEnumerable<Podcast>> GetPodcastsAsync()
        {
            var podcasts = await storageService.LoadAsync();
            foreach (var podcast in podcasts)
            {
                podcast.Episodes = podcast.Episodes.OrderByDescending(e => e.Published).ToList();
            }
            return podcasts;
        }

        public async Task<IEnumerable<Episode>> GetNewEpisodesAsync(Podcast podcast)
        {
            try
            {
                Podcast newPodcast = await feedParser.GetPodcastAsync(podcast.FeedUrl.ToString());
                var newEpisodes = new List<Episode>();

                foreach (var episode in newPodcast.Episodes)
                {
                    if (!podcast.Episodes.Contains(episode))
                    {
                        newEpisodes.Add(episode);
                    }
                }

                return newEpisodes.OrderBy(e => e.Published);
            }
            catch (Exception ex)
            {
                throw new GetPodcastException(podcast.FeedUrl.ToString(), ex);
            }
        }

        public async Task SavePodcastAsync(Podcast podcast)
        {
            await storageService.SaveAsync(podcast);
        }

        public async Task SavePodcastsAsync(IEnumerable<Podcast> podcasts)
        {
            await storageService.SaveAsync(podcasts);
        }

        public async Task DownloadEpisodeAsync(Episode episode)
        {
            var file = await fileDownloadManager.DownloadFileAsync(
                appFolderName: AppName,
                folderName: episode.Podcast.Title,
                fileName: string.Format("{0:dd.MM.yyyy} - {1} - {2}", episode.Published, episode.Author, episode.Title),
                fileUrl: episode.Path,
                callback: c =>
                {
                    var bytesReceived = c.Progress.BytesReceived;
                    var bytesToReceive = c.Progress.TotalBytesToReceive;
                    double percent = (bytesReceived * 100) / bytesToReceive;

                    Debug.WriteLine(string.Format("Received: {0}/{1} ({2:P1})", bytesReceived, bytesToReceive, percent / 100.0));
                },
                errorCallback: ex =>
                {
                    Debug.WriteLine(string.Format(ex.Message));
                }
                );

            if (file != null)
            {
                var podcast = episode.Podcast;
                var episodeToUpdate = podcast.Episodes.Where(e => e.Guid == episode.Guid).SingleOrDefault();

                episodeToUpdate.Path = file.Path;
                await storageService.SaveAsync(episode.Podcast);
            }
        }

        public Task<IEnumerable<Podcast>> SearchPodcast(string searchString)
        {
            throw new NotImplementedException();
        }

        public string AppName
        {
            get { return "Solocast"; }
        }
    }
}
