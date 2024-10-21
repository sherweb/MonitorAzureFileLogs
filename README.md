# MonitorAzureFileLogs
Azure Function that monitors log files in a Blob Storage container. It identifies file download events based on logs, and deletes the downloaded files from an Azure File Share. The function is triggered by a timer, running at regular intervals
