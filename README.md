# A Serilog sink sending log files to Amazon S3

## Serilog.Sinks.AmazonS3.Uploader

[![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.AmazonS3.Uploader.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.AmazonS3.Uploader/) 
[![NuGet](https://img.shields.io/nuget/dt/Serilog.Sinks.AmazonS3.Uploader.svg)](https://www.nuget.org/packages/Serilog.Sinks.AmazonS3.Uploader/)
[![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki)
[![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)
[![Help](https://img.shields.io/badge/stackoverflow-serilog-orange.svg)](http://stackoverflow.com/questions/tagged/serilog)

__Package__ - [Serilog.Sinks.AmazonS3.Uploader](https://www.nuget.org/packages/Serilog.Sinks.AmazonS3.Uploader)
| __Platforms__ - NET 5.0

### Installation

If you want to include the sink in your project, you can [install it directly from NuGet](https://www.nuget.org/packages/Serilog.Sinks.AmazonS3.Uploader/).

To install the sink, run the following command in the Package Manager Console:

```
PM> Install-Package Serilog.Sinks.AmazonS3.Uploader
```

# Use cases

This sink should be used when you already have a process which is producing your log files and you want to upload these files to S3 for archiving.
Once file is uploaded - it is deleted from original location.

NOTE: this process heavily depends on file locking. Normally serilog keeps current file locked and prevents access to it. So file cannot be uploaded while file is locked. However when file is rolled over (by time or size limitations) it is unlocked. Then this sink is able to upload the file to S3.

# Usage

In the following example the sink will read files in specified directory (c:/temp/logs) and sends them to S3 bucket (application-logs). Please note that it should exists another logger which produces log files and AmazonS3.Uploader does not write any local files nor it collects log events.

Used in conjunction with [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) the same sink can be configured in the following way:

```json
{
    "Serilog": {
        "MinimumLevel": {"Default": "Debug"},
        "WriteTo": [
            {
                "Name": "File",
                "Args": {
                    "path": "c:/temp/logs/application.log",
                    "rollingInterval": "Day",
                    "fileSizeLimitBytes": "104857600",
                    "rollOnFileSizeLimit": true
                }
            },
            {
                "Name": "S3",
                "Args": {
                    "logFileFolder": "c:/temp/logs",
                    "bucketName": "application-logs",
                    "accessKey": "",
                    "secretKey": "",
                    "region": "eu-west-1",
                    "s3Path": "dev/%timestamp%/application",
                    "filePrefix": "machine-name",
                    "period": "01:00:00"
                }
            }
        ]
    },
}
```

### Parameters

- logFileFolder > folder to watch for available files
- bucketName > S3 bucket name where to upload files
- accessKey > Amazon access key
- secretKey > Amazon secret key
- region > S3 bucket region
- s3Path > folder inside S3 where to put uploaded files
  - %timestamp% - formats file last write time as `yyyyMM`
- filePrefix > adds a prefix to a file, usefull when multiple instances of the same service are running, in that case prefix should be uniquely identifying instance
- period > defines how often to check for available files

