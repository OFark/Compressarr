namespace Compressarr.Filtering
{
    public enum FilterPropertyType
    {
        Boolean,
        DateTime,
        String,
        Number,
        Enum
    }

    public enum MediaSource
    {
        Radarr,
        Sonarr,
        Folder
    }
}