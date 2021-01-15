using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Extensions.Services;
using System.Threading.Tasks;
using Blazor.YouTubeDownloader.Api.Models;
using Matroska.Muxer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Blazor.YouTubeDownloader.Api.Functions
{
    internal class ApiFunctions
    {
        private readonly ILogger<ApiFunctions> _logger;
        private readonly YoutubeClient _client;
        private readonly ISerializer _serializer;

        public ApiFunctions(ILogger<ApiFunctions> logger, YoutubeClient client, ISerializer serializer)
        {
            _logger = logger;
            _client = client;
            _serializer = serializer;
        }

        [FunctionName("GetVideoMetaData")]
        public async Task<IActionResult> GetVideoMetaDataAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("HttpTrigger - GetVideoMetaDataAsync");

            string url = req.Query["YouTubeUrl"].Single();

            var videoMetaData = await _client.Videos.GetAsync(url);

            return new SystemTextJsonResult(videoMetaData, new JsonSerializerOptions { Converters = { new JsonTimeSpanConverter() } });
        }

        [FunctionName("GetAudioOnlyStreams")]
        public async Task<IActionResult> GetAudioOnlyStreamsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("HttpTrigger - GetAudioOnlyStreamsAsync");

            string url = req.Query["YouTubeUrl"].Single();

            var manifest = await _client.Videos.Streams.GetManifestAsync(url);

            var audioStreams = manifest.GetAudioOnly().OrderBy(a => a.Bitrate);

            return new SystemTextJsonResult(audioStreams);
        }

        [FunctionName("GetAudioStream")]
        public async Task<Stream> GetAudioStreamAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("HttpTrigger - GetAudioStreamAsync");

            var streamInfo = await _serializer.DeserializeAsync<AudioOnlyStreamInfo>(req.Body);

            return await _client.Videos.Streams.GetAsync(streamInfo);
        }

        [FunctionName("GetOggOpusAudioStream")]
        public async Task<Stream> GetOggOpusAudioStreamAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("HttpTrigger - GetOggOpusAudioStreamAsync");

            var streamInfo = await _serializer.DeserializeAsync<AudioOnlyStreamInfo>(req.Body);

            using var destinationStream = new MemoryStream();

            await _client.Videos.Streams.CopyToAsync(streamInfo, destinationStream);
            destinationStream.Position = 0;

            var oggOpusStream = new MemoryStream();
            MatroskaDemuxer.ExtractOggOpusAudio(destinationStream, oggOpusStream);

            oggOpusStream.Position = 0;
            return oggOpusStream;
        }

        [FunctionName("GetAudioBytes")]
        public async Task<byte[]> GetAudioBytesAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("HttpTrigger - GetAudioBytesAsync");

            var streamInfo = await _serializer.DeserializeAsync<AudioOnlyStreamInfo>(req.Body);

            using var destinationStream = new MemoryStream();

            await _client.Videos.Streams.CopyToAsync(streamInfo, destinationStream);

            return destinationStream.ToArray();
        }
    }
}