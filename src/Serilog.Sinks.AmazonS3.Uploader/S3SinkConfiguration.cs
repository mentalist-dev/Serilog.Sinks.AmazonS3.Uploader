using System;
using Serilog.Configuration;
using Serilog.Sinks.AmazonS3.Uploader;

// ReSharper disable once CheckNamespace
namespace Serilog
{
    public static class S3SinkConfiguration
    {
        public static LoggerConfiguration S3(this LoggerSinkConfiguration sinkConfiguration
            , string logFileFolder
            , string bucketName
            , string accessKey
            , string secretKey
            , string region
            , string? s3Path = null
            , string? filePrefix = null
            , TimeSpan? period = null)
        {
            period ??= TimeSpan.FromHours(1);

            var sink = new S3Sink(logFileFolder
                , bucketName
                , accessKey
                , secretKey
                , region
                , s3Path
                , filePrefix
                , period.Value
            );

            return sinkConfiguration.Sink(sink);
        }
    }
}
