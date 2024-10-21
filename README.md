# MonitorAzureFileLogs
The `MonitorFileLogs` function is designed to monitor logs within a specific Blob Storage container, particularly logs generated from Azure Storage Analytics. These logs capture file access events, such as file downloads, from an Azure File Share. The function is triggered at regular intervals using a timer (configurable in the local.settings.json file), and upon execution, it scans the logs to identify files that have been downloaded.

Once download events are detected, the function extracts the folder and file names from the log entries and attempts to delete the corresponding files from the Azure File Share. This automated process helps manage file lifecycle by removing files once they have been accessed. The function integrates with both Blob Storage for log reading 
and Azure File Share for file deletion, while logging all actions taken, including any errors encountered. 

This solution ensures efficient post-download file cleanup and assists in maintaining storage hygiene.
