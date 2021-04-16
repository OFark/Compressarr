# Compressarr
A tool that integrates with Radarr and Sonarr and used FFmpeg to recompress items from your library

## Source
Radarr and Sonarr items can be filtered to a selection and then processed. The Job remembers the filter not teh files so any new files that match the filter can be processed later. 

## FFmpeg
Presets are defined to tell FFmpeg what to do with each file from the filter. This is complete control over FFmpeg. Audio, Video, mapping, any codec installed on your computer you can use. The only automated thing is 2 pass on fixed bitrate, otherwise you should be able to do anything that FFmpeg can do.
FFmpeg is automatically updated by the application.

### Known issues:
Currently Sonnarr will connect, but theres no interface to build a filter.
