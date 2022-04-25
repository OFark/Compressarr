namespace Compressarr.Filtering
{
    public enum FilterPropertyType
    {
        Boolean,
        DateTime,
        String,
        Number,
        Enum,
        FileSize
    }

    public enum MediaSource
    {
        Radarr,
        Sonarr,
        Folder
    }
}