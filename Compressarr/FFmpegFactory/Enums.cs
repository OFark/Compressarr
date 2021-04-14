namespace Compressarr.FFmpegFactory
{
    public enum FFmpegStatus
    {
        Initialising,
        Ready
    }

    public enum CodecType
    {
        Audio,
        Subtitle,
        Video
    }

    public enum CodecOptionType
    {
        Number,
        Range,
        Select, 
        String
    }

    public enum h26xPreset
    {
        ultrafast,
        superfast,
        veryfast,
        faster,
        fast,
        medium,
        slow,
        slower,
        veryslow,
        placebo
    }

    public enum h264tune
    {
        animation,
        fastdecode,
        film,
        grain,
        stillimage,
        zerolatency
    }

    public enum h265tune
    {
        animation,
        fastdecode,
        grain,
        zerolatency
    }
}