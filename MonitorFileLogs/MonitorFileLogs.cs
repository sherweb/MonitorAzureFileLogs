using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Azure.Storage;

namespace MonitorFileLogs
{
    public class MonitorFileLogs
    {
        private readonly ILogger _logger;

        public MonitorFileLogs(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MonitorFileLogs>();
        }

        [Function("MonitorFileLogs")]
        public async Task Run([TimerTrigger("%TimerSchedule%")] TimerInfo myTimer)
        {
            var logStorageAccountName = Environment.GetEnvironmentVariable("LogStorageAccountName");
            var logStorageAccountKey = Environment.GetEnvironmentVariable("LogStorageAccountKey");
            var logContainerName = Environment.GetEnvironmentVariable("LogContainerName");

            var fileShareStorageAccountName = Environment.GetEnvironmentVariable("FileShareStorageAccountName");
            var fileShareStorageAccountKey = Environment.GetEnvironmentVariable("FileShareStorageAccountKey");
            var fileShareName = Environment.GetEnvironmentVariable("FileShareName");

            // Access the log storage account
            var logBlobServiceClient = new BlobServiceClient(
                new Uri($"https://{logStorageAccountName}.blob.core.windows.net"),
                new StorageSharedKeyCredential(logStorageAccountName, logStorageAccountKey));

            var logContainer = logBlobServiceClient.GetBlobContainerClient(logContainerName); 

            // Access the file share storage account
            var fileShareServiceClient = new ShareServiceClient(
                new Uri($"https://{fileShareStorageAccountName}.file.core.windows.net"),
                new StorageSharedKeyCredential(fileShareStorageAccountName, fileShareStorageAccountKey));
            var fileShareClient = fileShareServiceClient.GetShareClient(fileShareName); // Change to your file share name

            // Iterate through the blobs in the log container
            await foreach (var blobItem in logContainer.GetBlobsAsync())
            {
                var blobClient = logContainer.GetBlobClient(blobItem.Name);
                var logContent = await blobClient.DownloadContentAsync();

                var filesDownloaded = FindFilesDownloaded(logContent.Value.Content.ToString());

                foreach (var fileDownloaded in filesDownloaded)
                {
                    if (!string.IsNullOrEmpty(fileDownloaded.FileName))
                    {
                        _logger.LogInformation($"File '{fileDownloaded.FileName}' in folder '{fileDownloaded.FolderName}' was downloaded. Attempting to delete it from the file share...");

                        await DeleteFileFromFileShare(fileDownloaded.FolderName, fileDownloaded.FileName, fileShareClient);
                    }
                    else
                    {
                        _logger.LogWarning("No valid file information found in the log entry.");
                    }
                }
            }
        }

        private static List<FileInfo> FindFilesDownloaded(string logContent)
        {
            var downloadRegex = new Regex(@"""operationName"":\s*""GetFile"".*?""uri"":\s*""([^""]+)""", RegexOptions.IgnoreCase);
            var matches = downloadRegex.Matches(logContent);

            var downloadedFiles = new List<FileInfo>();

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var uri = match.Groups[1].Value;

                    var folderName = GetFolderNameFromUri(uri);
                    var fileName = GetFileNameFromUri(uri);

                    downloadedFiles.Add(new FileInfo(folderName, fileName));
                }
            }

            return downloadedFiles;
        }

        private static string GetFileNameFromUri(string uri)
        {
            var fileUri = new Uri(uri);
            var path = fileUri.AbsolutePath;
            return Path.GetFileName(Uri.UnescapeDataString(path));
        }

        private static string GetFolderNameFromUri(string uri)
        {
            var fileShareName = Environment.GetEnvironmentVariable("FileShareName");

            var shareIndex = uri.IndexOf(fileShareName, StringComparison.OrdinalIgnoreCase);

            if (shareIndex >= 0)
            {
                var subPath = uri.Substring(shareIndex + fileShareName.Length);

                var segments = subPath.Split('/');

                if (segments.Length > 1)
                {
                    return Uri.UnescapeDataString(string.Join("/", segments.Take(segments.Length - 1)));
                }
            }

            return null;
        }

        private async Task DeleteFileFromFileShare(string folderName, string fileName, ShareClient fileShareClient)
        {
            var directoryClient = fileShareClient.GetDirectoryClient(folderName);
            var fileClient = directoryClient.GetFileClient(fileName);

            try
            {
                await fileClient.DeleteIfExistsAsync();
                _logger.LogInformation($"File '{fileName}' deleted successfully from the file share.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete file '{fileName}': {ex.Message}");
            }
        }
    }

    public class FileInfo(string folderName, string fileName)
    {
        public string FolderName { get; private set; } = folderName;
        public string FileName { get; private set; } = fileName;
    };
}
