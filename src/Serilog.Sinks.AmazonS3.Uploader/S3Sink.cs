using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.AmazonS3.Uploader
{
    public class S3Sink : ILogEventSink
    {
        private static readonly string HostName = Dns.GetHostName();
        private static readonly Channel<DateTime> Events = Channel.CreateBounded<DateTime>(100);

        private readonly string _logFileFolder;
        private readonly string _bucketName;
        private readonly TimeSpan _period;
        private readonly string? _s3Path;
        private readonly string? _filePrefix;
        private readonly AmazonS3Client? _client;
        // ReSharper disable once NotAccessedField.Local
        private readonly Task? _consumerTask;

        public S3Sink(string logFileFolder
            , string bucketName
            , string accessKey
            , string secretKey
            , string region
            , string? s3Path
            , string? filePrefix
            , TimeSpan period)
        {
            _logFileFolder = logFileFolder;
            _bucketName = bucketName;
            _period = period;
            _s3Path = s3Path;
            _filePrefix = filePrefix;

            IsEnabled = !string.IsNullOrWhiteSpace(_logFileFolder) &&
                        !string.IsNullOrWhiteSpace(bucketName) &&
                        !string.IsNullOrWhiteSpace(accessKey) &&
                        !string.IsNullOrWhiteSpace(secretKey) &&
                        !string.IsNullOrWhiteSpace(region);

            if (IsEnabled)
            {
                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                _client = new AmazonS3Client(credentials, regionEndpoint);

                _consumerTask = Task.Factory.StartNew(Consume,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning);
            }
            else
            {
                SelfLog.WriteLine("S3Sink is disabled as one of required parameters are not specified: [logFileFolder, bucketName, accessKey, secretKey, region]");
            }
        }

        private bool IsEnabled { get; }

        public void Emit(LogEvent logEvent)
        {
            if (IsEnabled)
            {
                Events.Writer.TryWrite(DateTime.UtcNow);
            }
        }

        private async Task Consume()
        {
            var lastCheckTime = DateTime.MinValue;

            try
            {
                await foreach (var timestamp in Events.Reader.ReadAllAsync(CancellationToken.None))
                {
                    if (timestamp - lastCheckTime > _period)
                    {
                        try
                        {
                            await CheckFilesToUpload();
                        }
                        catch (Exception e)
                        {
                            SelfLog.WriteLine($"{e.Message} {e.StackTrace}");
                        }
                        finally
                        {
                            lastCheckTime = timestamp;
                        }
                    }
                }
            }
            finally
            {
                Events.Writer.TryComplete();
            }
        }

        private async Task CheckFilesToUpload()
        {
            if (string.IsNullOrWhiteSpace(_logFileFolder))
                return;

            var directory = new DirectoryInfo(_logFileFolder);
            if (!directory.Exists)
                return;

            var today = DateTime.UtcNow.Date;
            var files = directory.GetFiles();

            var filesToUpload = new List<FileInfo>();

            foreach (var file in files)
            {
                if (!file.FullName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.LastWriteTimeUtc >= today)
                {
                    continue;
                }

                filesToUpload.Add(file);
            }

            if (filesToUpload.Count > 0)
            {
                using var fileTransferUtility = new TransferUtility(_client);
                foreach (var file in filesToUpload)
                {
                    FileStream stream;
                    try
                    {
                        stream = file.OpenRead();
                    }
                    catch
                    {
                        // unable to open file (maybe still in use?)
                        continue;
                    }

                    try
                    {
                        var key = GetS3Key(file);
                        await fileTransferUtility.UploadAsync(stream, _bucketName, key).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        SelfLog.WriteLine($"{e.GetType()}: {e.Message} {e.StackTrace}");
                    }
                    finally
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }

                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // SelfLog.WriteLine($"{e.GetType()}: {e.Message} {e.StackTrace}");
                    }
                }
            }
        }

        private string GetS3Key(FileInfo file)
        {
            var timeFolder = $"{file.LastWriteTimeUtc:yyyyMM}";

            var fileName = file.Name;
            if (!string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(_filePrefix))
            {
                var prefix = _filePrefix;
                if (prefix.Contains("%timestamp%"))
                {
                    prefix = prefix.Replace("%timestamp%", $"{DateTime.UtcNow:yyyyMMddHHmmss}");
                }
                if (prefix.Contains("%hostname%"))
                {
                    prefix = prefix.Replace("%hostname%", HostName);
                }

                fileName = $"{prefix}-{file.Name}";
            }

            var key = $"{timeFolder}/{fileName}";

            if (!string.IsNullOrWhiteSpace(_s3Path))
            {
                var path = _s3Path;
                path = path.Contains("%timestamp%")
                    ? path.Replace("%timestamp%", timeFolder)
                    : $"{timeFolder}/{path}";

                key = $"{path}/{fileName}";
            }

            return key;
        }
    }
}
